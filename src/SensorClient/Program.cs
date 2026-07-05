using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Shared;

var configPath = Environment.GetEnvironmentVariable("SENSOR_CONFIG_PATH") ??
                 args.FirstOrDefault() ??
                 "sensor-config.json";

if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"Sensor configuration file was not found: {configPath}");
    return;
}

var serializerOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};
serializerOptions.Converters.Add(new JsonStringEnumConverter());

var config = JsonSerializer.Deserialize<SensorClientConfiguration>(await File.ReadAllTextAsync(configPath), serializerOptions);
if (config is null)
{
    Console.Error.WriteLine("Failed to load sensor configuration.");
    return;
}

using var runtime = new SensorRuntime(config, serializerOptions);
using var cancellationSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

await runtime.RunAsync(cancellationSource.Token);

internal sealed class SensorRuntime : IDisposable
{
    private readonly SensorClientConfiguration _config;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly HttpClient _httpClient = new();
    private readonly object _stateLock = new();
    private readonly Random _random = new();
    private readonly HubConnection _connection;
    private readonly RSA _sensorPrivateKey;
    private readonly RSA _serverPublicKey;
    private long _nextMessageId;
    private bool _isActive;
    private bool _awaitingReset;
    private SensorMode _mode;

    public SensorRuntime(SensorClientConfiguration config, JsonSerializerOptions serializerOptions)
    {
        _config = config;
        _serializerOptions = serializerOptions;
        _mode = config.Mode;
        _sensorPrivateKey = SecureMessageCrypto.LoadPrivateKeyFromFile(config.Crypto.SensorPrivateKeyPath);
        _serverPublicKey = SecureMessageCrypto.LoadPublicKeyFromFile(config.Crypto.ServerPublicKeyPath);
        _connection = new HubConnectionBuilder()
            .WithUrl(config.Server.NotificationHubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On("Activate", () =>
        {
            lock (_stateLock)
            {
                if (!_awaitingReset)
                {
                    _isActive = true;
                }
            }

            WriteInfo($"SignalR command received: Activate for {_config.SensorId}.");
        });

        _connection.On("Deactivate", () =>
        {
            lock (_stateLock)
            {
                _isActive = false;
            }

            WriteInfo($"SignalR command received: Deactivate for {_config.SensorId}.");
        });

        _connection.On<AlarmNotificationRequest>("Alarm", request =>
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = AlarmEvaluator.ToConsoleColor(request.Priority);
            Console.WriteLine($"[{DateTimeOffset.Now:O}] Alarm from {request.Source}: value={request.Value:F2}, priority={request.Priority}, consensus={request.IsConsensus}");
            Console.ForegroundColor = previousColor;
        });

        _connection.On<StatusNotificationRequest>("StatusChanged", request =>
        {
            lock (_stateLock)
            {
                _isActive = request.Status == SensorStatus.Active;
                _awaitingReset = request.Status is SensorStatus.InactiveBlocked or SensorStatus.DosBlocked or SensorStatus.Bad;
            }

            WriteInfo($"StatusChanged: {request.SensorId} -> {request.Status}. {request.Message}");
        });

        _connection.On<ConsensusNotificationRequest>("ConsensusCalculated", request =>
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = AlarmEvaluator.ToConsoleColor(request.Priority);
            Console.WriteLine(
                $"[{DateTimeOffset.Now:O}] ConsensusCalculated: value={request.Value:F2}, priority={request.Priority}, badSensors=[{string.Join(", ", request.BadSensors)}]");
            Console.ForegroundColor = previousColor;
        });

        _connection.Reconnected += async _ =>
        {
            await _connection.InvokeAsync("RegisterSensorConnection", _config.SensorId);
            WriteInfo("SignalR reconnected and sensor mapping refreshed.");
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await RegisterAsync(cancellationToken);
        await ConnectSignalRAsync(cancellationToken);

        var senderTask = RunSenderLoopAsync(cancellationToken);
        var commandTask = RunCommandLoopAsync(cancellationToken);
        await Task.WhenAll(senderTask, commandTask);
    }

    public void Dispose()
    {
        try
        {
            _connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort shutdown for interactive console app.
        }

        _httpClient.Dispose();
        _sensorPrivateKey.Dispose();
        _serverPublicKey.Dispose();
    }

    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        var publicKeyPem = await File.ReadAllTextAsync(_config.Crypto.SensorPublicKeyPath, cancellationToken);
        var request = new SensorRegistrationRequest
        {
            SensorId = _config.SensorId,
            InitialDataQuality = _config.InitialDataQuality,
            PublicKeyPem = publicKeyPem,
            TemperatureGeneration = _config.TemperatureGeneration,
            AlarmThresholds = _config.AlarmThresholds
        };

        WriteInfo($"Registering sensor {_config.SensorId} at {_config.Server.IngressBaseUrl}.");

        var response = await _httpClient.PostAsJsonAsync(
            $"{_config.Server.IngressBaseUrl.TrimEnd('/')}/api/ingest/register",
            request,
            _serializerOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            lock (_stateLock)
            {
                _isActive = false;
                _awaitingReset = true;
            }

            WriteInfo($"Registration failed with HTTP {(int)response.StatusCode}.");
            return;
        }

        var body = await response.Content.ReadFromJsonAsync<SensorRegistrationResponse>(_serializerOptions, cancellationToken);
        if (body is null)
        {
            WriteInfo("Registration returned an empty response.");
            return;
        }

        lock (_stateLock)
        {
            _isActive = body.Accepted && body.Status == SensorStatus.Active;
            _awaitingReset = !body.Accepted;
        }

        if (body.Accepted)
        {
            WriteInfo($"Registration accepted. Status: {body.Status}.");
        }
        else
        {
            WriteInfo($"Registration rejected: {body.Reason} RetryAfter={body.RetryAfterSeconds}");
        }
    }

    private async Task ConnectSignalRAsync(CancellationToken cancellationToken)
    {
        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync("RegisterSensorConnection", _config.SensorId, cancellationToken);
        WriteInfo($"Connected to NotificationService hub: {_config.Server.NotificationHubUrl}");
    }

    private async Task RunSenderLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = SnapshotState();
            if (!snapshot.IsActive || snapshot.AwaitingReset)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                continue;
            }

            if (snapshot.Mode == SensorMode.Blocked)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                continue;
            }

            if (snapshot.Mode == SensorMode.Dos)
            {
                for (var index = 0; index < 15; index++)
                {
                    await SendMeasurementAsync(snapshot.Mode, cancellationToken);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                continue;
            }

            await SendMeasurementAsync(snapshot.Mode, cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(1000, 10001)), cancellationToken);
        }
    }

    private async Task RunCommandLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var command = await Task.Run(() => Console.ReadLine(), cancellationToken);
            if (command is null)
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            switch (command.Trim())
            {
                case "/dos":
                    SetMode(SensorMode.Dos, "Mode changed to DoS flood.");
                    break;
                case "/bad":
                    SetMode(SensorMode.Bad, "Mode changed to BAD values.");
                    break;
                case "/bad --signature":
                    SetMode(SensorMode.BadSignature, "Mode changed to invalid signature.");
                    break;
                case "/blocked":
                    await EnterBlockedModeAsync(cancellationToken);
                    break;
                case "/reset":
                    await ResetAsync(cancellationToken);
                    break;
                default:
                    WriteInfo("Unknown command. Supported: /dos, /bad, /bad --signature, /blocked, /reset");
                    break;
            }
        }
    }

    private async Task SendMeasurementAsync(SensorMode mode, CancellationToken cancellationToken)
    {
        var messageId = NextMessageId();
        var value = mode switch
        {
            SensorMode.Bad or SensorMode.BadSignature => _config.TemperatureGeneration.Max + 100.0,
            _ => _config.TemperatureGeneration.Min +
                 (_random.NextDouble() * (_config.TemperatureGeneration.Max - _config.TemperatureGeneration.Min))
        };

        var payload = new MeasurementPayloadDto
        {
            SensorId = _config.SensorId,
            MessageId = messageId,
            Timestamp = DateTimeOffset.UtcNow,
            Value = value,
            AlarmPriority = AlarmEvaluator.Evaluate(value, _config.AlarmThresholds),
            DataQuality = _config.InitialDataQuality
        };

        PrintMeasurement(payload);

        var envelope = SecureMessageCrypto.EncryptAndSign(_config.SensorId, payload, _serverPublicKey, _sensorPrivateKey);
        if (mode == SensorMode.BadSignature)
        {
            envelope = new SecureEnvelopeDto
            {
                SensorId = envelope.SensorId,
                EncryptedAesKey = envelope.EncryptedAesKey,
                Iv = envelope.Iv,
                CipherText = envelope.CipherText,
                Tag = envelope.Tag,
                Signature = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            };
        }

        var response = await _httpClient.PostAsJsonAsync(
            $"{_config.Server.IngressBaseUrl.TrimEnd('/')}/api/ingest/measurements",
            envelope,
            _serializerOptions,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        WriteInfo($"Measurement rejected ({(int)response.StatusCode}): {responseText}");
    }

    private async Task EnterBlockedModeAsync(CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            _mode = SensorMode.Blocked;
            _isActive = false;
            _awaitingReset = true;
        }

        WriteInfo("Mode changed to Blocked. Sensor will stop sending for 30 seconds and then wait for /reset.");

        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        WriteInfo("Blocked interval elapsed. Sensor remains inactive until /reset.");
    }

    private async Task ResetAsync(CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            _mode = SensorMode.Normal;
            _nextMessageId = 0;
            _awaitingReset = false;
            _isActive = false;
        }

        WriteInfo("Resetting sensor state and re-registering.");
        await RegisterAsync(cancellationToken);
    }

    private void SetMode(SensorMode mode, string message)
    {
        lock (_stateLock)
        {
            _mode = mode;
        }

        WriteInfo(message);
    }

    private long NextMessageId()
    {
        lock (_stateLock)
        {
            return _nextMessageId++;
        }
    }

    private (bool IsActive, bool AwaitingReset, SensorMode Mode) SnapshotState()
    {
        lock (_stateLock)
        {
            return (_isActive, _awaitingReset, _mode);
        }
    }

    private void PrintMeasurement(MeasurementPayloadDto payload)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = AlarmEvaluator.ToConsoleColor(payload.AlarmPriority);
        Console.WriteLine(
            $"[{payload.Timestamp:O}] {_config.SensorId} -> value={payload.Value:F2}, messageId={payload.MessageId}, priority={payload.AlarmPriority}");
        Console.ForegroundColor = previousColor;
    }

    private static void WriteInfo(string message)
    {
        Console.WriteLine($"[{DateTimeOffset.Now:O}] {message}");
    }
}
