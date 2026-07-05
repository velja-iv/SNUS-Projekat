# Distributed Sensor Monitoring System

## 1. Project overview
This repository implements a distributed sensor monitoring system in C# / .NET 10 for collecting, storing, processing and reporting temperature data from sensor clients. The system keeps exactly five active sensors when enough valid sensors exist, stores raw measurements in PostgreSQL, calculates periodic consensus values, raises alarms, and exposes read-only reporting endpoints.

## 2. Architecture
- `SensorClient`: one console process/container per sensor
- `Ingress`: YARP reverse proxy entry point
- `IngestionService`: registration, secure measurement ingestion, replay checks, rate limiting and inactive-sensor failover
- `ConsensusWorker`: minute-based BFT-inspired trimmed-average consensus and bad-sensor detection
- `NotificationService`: SignalR hub plus HTTP endpoints for activation/status/alarm fan-out
- `ReportsService`: read-only historical and registry reporting
- `Shared`: DTOs, enums, EF Core model and crypto helpers

## 3. Services
- `POST /api/ingest/register`
- `POST /api/ingest/measurements`
- `POST /api/ingest/events/consensus`
- `GET /api/ingest/health`
- `GET /api/reports/measurements?sensorId=&from=&to=`
- `GET /api/reports/consensus?from=&to=`
- `GET /api/reports/alarms?from=&to=`
- `GET /api/reports/sensors`
- `GET /api/reports/sensors/active`
- `GET /api/reports/sensors/bad`
- SignalR hub: `/hubs/sensors`

## 4. Data model
- `Sensors`: registry, public keys, status, data quality, last message timestamps and fault counters
- `SensorMeasurements`: raw accepted measurements with replay/signature markers and raw alarm priority
- `ConsensusMeasurements`: one row per processed minute with consensus value, counts and system alarm priority

## 5. Security model
- Payload encryption uses `AES-GCM`
- AES session key encryption uses `RSA-OAEP`
- Digital signatures use `RSA`
- Replay protection uses per-message `timestamp` and monotonic `messageId`
- The outer `sensorId` stays unencrypted so rate limiting can run before decryption
- Trust-on-first-use stores the first public key seen for each `SensorId`
- A reused `SensorId` with a different public key is rejected
- DoS detection blocks sensors that exceed `10` messages per second

### Security limitations
- Trust-on-first-use is weaker than a proper PKI or certificate enrollment flow
- Metadata is still visible over plain HTTP even though payload contents are encrypted
- Production systems should add HTTPS and ideally mTLS

## 6. Consensus algorithm
- The worker reads raw measurements from the previous minute
- Only sensors with current `DataQuality = GOOD` enter consensus
- Measurements are averaged per sensor first
- A trimmed average removes a configurable percentage from both ends
- If fewer than `3` GOOD sensor averages exist, the minute is skipped
- Sensors whose per-minute average deviates beyond the configured threshold accumulate incidents and can become `BAD`

## 7. Fault tolerance behavior
- Required active sensor count defaults to `5`
- Active sensors that stop sending for more than `10` seconds become `InactiveBlocked`
- The first registered `GOOD` standby sensor is promoted to `Active`
- `/blocked` simulates a 30-second pause and requires `/reset` before resuming

## 8. Docker Compose run instructions
1. Generate keys:
   `powershell -ExecutionPolicy Bypass -File .\scripts\generate-keys.ps1`
2. Build locally if desired:
   `dotnet build .\MeasurementCollector.slnx`
3. Start the stack:
   `docker compose -f .\docker\docker-compose.yml up --build`
4. Main ports:
   `8080` ingress
   `8081` ingestion
   `8082` notification hub
   `8083` reports

## 9. Kubernetes / Minikube run instructions
1. Enable ingress:
   `minikube addons enable ingress`
2. Apply manifests:
   `kubectl apply -f k8s/postgres.yaml`
   `kubectl apply -f k8s/notification-deployment.yaml`
   `kubectl apply -f k8s/ingestion-deployment.yaml`
   `kubectl apply -f k8s/reports-deployment.yaml`
   `kubectl apply -f k8s/consensus-deployment.yaml`
   `kubectl apply -f k8s/ingress-deployment.yaml`
   `kubectl apply -f k8s/ingress.yaml`
   `kubectl apply -f k8s/sensor-client.yaml`
3. Get the Minikube IP:
   `minikube ip`
4. Create the `sensor-crypto-keys` secret from generated PEM files before deploying the services that mount `/app/keys`.

## 10. Two-computer demonstration setup
- Computer A runs PostgreSQL, `IngestionService`, `ConsensusWorker`, `NotificationService`, `ReportsService` and `Ingress`
- Computer B runs one or more `SensorClient` processes or containers
- Replace client config URLs with the LAN IP of Computer A, for example:
  - `IngressBaseUrl = http://192.168.1.20:8080`
  - `NotificationHubUrl = http://192.168.1.20:8082/hubs/sensors`
- Do not use `localhost` across computers

## 11. Sensor console commands
- `/dos`
- `/bad`
- `/bad --signature`
- `/blocked`
- `/reset`

## 12. Demo scenarios
- Normal startup with 5 active sensors and standby reserve sensors
- `/blocked` on an active sensor to trigger reserve activation
- `/bad` to trigger consensus outlier detection
- `/bad --signature` to trigger signature rejection
- `/dos` to trigger rate limiting and permanent bad marking
- Reports endpoint calls to inspect raw values, consensus rows and alarms

## 13. Known limitations
- The implemented consensus is a simplified BFT-inspired trimmed average, not full PBFT
- EF schema initialization currently uses `EnsureCreated` during service startup instead of checked-in migrations
- Kubernetes manifests are intentionally pragmatic and expect prebuilt images plus a mounted key secret
- The sample `sensor-client.yaml` shows deployment patterns rather than all seven sensor deployments
