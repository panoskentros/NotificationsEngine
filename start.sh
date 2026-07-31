#!/bin/bash

trap 'kill $(jobs -p)' EXIT

docker compose up -d

dotnet run --project src/Infrastructure/NotificationEngine.Api &

dotnet run --project src/Gateway/NotificationEngine.Gateway/NotificationEngine.Gateway.csproj &

wait
