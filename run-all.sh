#!/bin/bash

(cd UserService && dotnet run > /tmp/userservice.log 2>&1) &
echo $! >> /tmp/service-pids.txt

(cd CatalogService && dotnet run > /tmp/catalogservice.log 2>&1) &
echo $! >> /tmp/service-pids.txt

(cd ReservationService && dotnet run > /tmp/reservationservice.log 2>&1) &
echo $! >> /tmp/service-pids.txt

wait