# Pokretanje Sistema: Windows Server + Linux Klijenti

Ovaj fajl je praktično uputstvo za pokretanje sistema na 2 računara:
- `Windows` računar: svi serverski servisi
- `Linux` računar: svi `SensorClient` klijenti

Ovo je preporučeni način za demonstraciju projekta.

## 1. Arhitektura pokretanja

Na `Windows` računaru se pokreću:
- `PostgreSQL`
- `IngestionService`
- `NotificationService`
- `ReportsService`
- `ConsensusWorker`
- `Ingress`

Na `Linux` računaru se pokreću:
- `sensor-01`
- `sensor-02`
- `sensor-03`
- `sensor-04`
- `sensor-05`
- `sensor-06`
- `sensor-07`

Komunikacija ide ovako:
- klijenti šalju HTTP poruke na `Ingress` preko porta `8080`
- klijenti se povezuju na SignalR hub preko `NotificationService` porta `8082`

## 2. Šta treba da bude instalirano

### Windows server
- `Git`
- `Docker Desktop` sa `docker compose`
- `PowerShell`
- `OpenSSL`

### Linux klijentski računar
- `Git`
- `.NET 10 SDK`

Provera na Linux-u:

```bash
dotnet --version
```

## 3. Priprema repozitorijuma

Na oba računara kloniraj isti repozitorijum.

Primer:

```bash
git clone <URL_REPOZITORIJUMA>
cd MeasurementCollector
```

Ako je ime foldera drugačije, koristi to ime umesto `MeasurementCollector`.

## 4. Pokretanje serverskog dela na Windows-u

### Korak 1: uđi u root projekta

Otvori terminal u root folderu projekta.

### Korak 2: generiši ključeve

Ovaj korak pravi:
- serverski privatni i javni ključ
- privatne i javne ključeve za svih 7 senzora

Pokreni:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\generate-keys.ps1
```

Napomena:
- ako ključevi već postoje, skripta ih neće prepisati
- ako baš želiš novi set ključeva, koristi `-Force`

Primer:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\generate-keys.ps1 -Force
```

### Korak 3: pripremi ključeve za Linux klijente

Ovaj korak kopira potrebne PEM fajlove u `linux-client/keys/`.

Pokreni:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\prepare-linux-client-keys.ps1
```

### Korak 4: pokreni backend servise

Najjednostavniji način:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-backend.ps1
```

Ova skripta:
- proverava root projekta
- generiše ključeve ako ne postoje
- podiže backend preko `docker-compose.backend.yml`

Ako želiš backend bez rebuild-a:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-backend.ps1 -NoBuild
```

Alternativno možeš direktno:

```powershell
docker compose -f .\docker\docker-compose.backend.yml up --build
```

### Korak 5: saznaj IP adresu Windows servera

Pokreni:

```powershell
ipconfig
```

Zapamti `IPv4 Address`, na primer:

```text
192.168.1.20
```

Tu adresu će koristiti Linux klijenti.

### Korak 6: otvori portove u firewall-u

Potrebni portovi:
- `8080` za `Ingress`
- `8082` za `NotificationService`
- opciono `8083` za direktan `ReportsService`

Bez ovoga Linux klijenti možda neće moći da se povežu.

## 5. Prebacivanje ključeva na Linux

Sa Windows računara na Linux računar prebaci sadržaj foldera:

```text
linux-client/keys/
```

U tom folderu treba da budu:
- `server-public.pem`
- `sensor-01-private.pem` i `sensor-01-public.pem`
- `sensor-02-private.pem` i `sensor-02-public.pem`
- `sensor-03-private.pem` i `sensor-03-public.pem`
- `sensor-04-private.pem` i `sensor-04-public.pem`
- `sensor-05-private.pem` i `sensor-05-public.pem`
- `sensor-06-private.pem` i `sensor-06-public.pem`
- `sensor-07-private.pem` i `sensor-07-public.pem`

Ne prebacuj server privatni ključ na Linux.

## 6. Priprema Linux klijenta

### Korak 1: uđi u root projekta

```bash
cd MeasurementCollector
```

### Korak 2: daj execute dozvole skriptama

```bash
chmod +x ./scripts/generate-linux-client-configs.sh
chmod +x ./scripts/start-sensor-linux.sh
```

### Korak 3: generiši runtime konfiguracije za pravi IP servera

Ako je IP Windows servera `192.168.1.20`, pokreni:

```bash
./scripts/generate-linux-client-configs.sh 192.168.1.20
```

Ova skripta:
- uzima template fajlove iz `linux-client/configs/`
- zamenjuje `__SERVER_IP__` pravom adresom
- smešta gotove fajlove u `linux-client/runtime-configs/`

## 7. Pokretanje senzora na Linux-u

Najbolje je da otvoriš `7 terminala`, po jedan za svaki senzor.

U svakom terminalu pokreni po jedan senzor:

```bash
./scripts/start-sensor-linux.sh 01
./scripts/start-sensor-linux.sh 02
./scripts/start-sensor-linux.sh 03
./scripts/start-sensor-linux.sh 04
./scripts/start-sensor-linux.sh 05
./scripts/start-sensor-linux.sh 06
./scripts/start-sensor-linux.sh 07
```

Zašto odvojeni terminali:
- možeš da vidiš log svakog senzora
- možeš ručno da kucaš komande `/blocked`, `/dos`, `/bad`, `/bad --signature`, `/reset`

## 8. Kako proveravaš da sistem radi

Na Windows serveru, u novom terminalu, proveri:

```powershell
Invoke-RestMethod http://localhost:8080/api/reports/sensors
Invoke-RestMethod http://localhost:8080/api/reports/measurements
Invoke-RestMethod http://localhost:8080/api/reports/consensus
Invoke-RestMethod http://localhost:8080/api/reports/alarms
```

Očekivano ponašanje:
- `sensor-01` do `sensor-05` postaju `Active`
- `sensor-06` postaje `Standby`
- `sensor-07` postaje `Standby` i `UNCERTAIN`
- `/measurements` počinje brzo da se puni
- `/consensus` dobija podatke nakon otprilike 1 minuta

## 9. Demo komande za testiranje

Na Linux klijentu, u konzoli senzora, možeš kucati:

- `/blocked`
  simulira da senzor ne šalje poruke 30 sekundi
- `/bad`
  šalje loše vrednosti, ali validno potpisane
- `/bad --signature`
  šalje poruku sa nevalidnim potpisom
- `/dos`
  pokušava da izazove DoS blokadu
- `/reset`
  vraća senzor u normalan režim i ponovo ga registruje

## 10. Očekivani test scenariji

### Scenario 1: normalan start
- pokreni backend
- pokreni 7 senzora na Linux-u
- proveri da li je 5 senzora aktivno

### Scenario 2: blokada aktivnog senzora
- na jednom aktivnom senzoru ukucaj `/blocked`
- posle oko `10` sekundi server treba da označi taj senzor kao neaktivan
- `sensor-06` treba da postane aktivan ako je prvi `GOOD` standby

### Scenario 3: loši podaci
- na nekom senzoru ukucaj `/bad`
- posle više konsenzus ciklusa taj senzor može biti označen kao `BAD`

### Scenario 4: nevalidan potpis
- ukucaj `/bad --signature`
- server treba da odbije poruku

### Scenario 5: DoS
- ukucaj `/dos`
- server treba da detektuje previše poruka i blokira taj senzor

## 11. Korisni logovi na Windows serveru

Za praćenje logova:

```powershell
docker compose -f .\docker\docker-compose.backend.yml logs -f ingestion-service
docker compose -f .\docker\docker-compose.backend.yml logs -f notification-service
docker compose -f .\docker\docker-compose.backend.yml logs -f consensus-worker
docker compose -f .\docker\docker-compose.backend.yml logs -f reports-service
docker compose -f .\docker\docker-compose.backend.yml logs -f ingress
```

## 12. Ako nešto ne radi

Prvo proveri ove stvari:
- da li backend kontejneri stvarno rade
- da li su `8080` i `8082` otvoreni
- da li Linux klijent koristi dobar IP servera
- da li su PEM fajlovi prebačeni u `linux-client/keys/`
- da li je `dotnet --version` na Linux-u zaista `10`
- da li si pokrenuo `generate-linux-client-configs.sh` pre starta senzora

## 13. Najkraći redosled bez objašnjenja

### Windows

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\generate-keys.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\prepare-linux-client-keys.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\start-backend.ps1
ipconfig
```

### Linux

```bash
chmod +x ./scripts/generate-linux-client-configs.sh
chmod +x ./scripts/start-sensor-linux.sh
./scripts/generate-linux-client-configs.sh 192.168.1.20
./scripts/start-sensor-linux.sh 01
./scripts/start-sensor-linux.sh 02
./scripts/start-sensor-linux.sh 03
./scripts/start-sensor-linux.sh 04
./scripts/start-sensor-linux.sh 05
./scripts/start-sensor-linux.sh 06
./scripts/start-sensor-linux.sh 07
```
