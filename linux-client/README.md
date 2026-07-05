# Linux Client Setup

This folder is intended for the client-side Linux machine in the two-computer deployment.

## Layout
- `configs/`: template sensor config files with `__SERVER_IP__` placeholder
- `keys/`: place generated PEM files here
- `runtime-configs/`: generated per-machine config files for the actual server IP

## Required files in `keys/`
- `server-public.pem`
- `sensor-01-private.pem` and `sensor-01-public.pem`
- `sensor-02-private.pem` and `sensor-02-public.pem`
- `sensor-03-private.pem` and `sensor-03-public.pem`
- `sensor-04-private.pem` and `sensor-04-public.pem`
- `sensor-05-private.pem` and `sensor-05-public.pem`
- `sensor-06-private.pem` and `sensor-06-public.pem`
- `sensor-07-private.pem` and `sensor-07-public.pem`

## Typical usage on Linux
1. Generate runtime configs:
   `./scripts/generate-linux-client-configs.sh 192.168.1.20`
2. Start one sensor in its own terminal:
   `./scripts/start-sensor-linux.sh 01`
3. Repeat for `02`, `03`, `04`, `05`, `06`, `07`

Use separate terminals if you want to type `/blocked`, `/dos`, `/bad`, or `/reset`.
