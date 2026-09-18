#!/bin/bash

(cd UserService && dotnet run --no-build --configuration Release > /tmp/userservice.log 2>&1) &
echo $! >> /tmp/service-pids.txt

(cd CatalogService && dotnet run --no-build --configuration Release > /tmp/catalogservice.log 2>&1) &
echo $! >> /tmp/service-pids.txt

(cd ReservationService && dotnet run --no-build --configuration Release > /tmp/reservationservice.log 2>&1) &
echo $! >> /tmp/service-pids.txt

wait