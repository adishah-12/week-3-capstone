#!/bin/bash
trap 'kill 0' EXIT

(cd UserService && dotnet run > /tmp/userservice.log 2>&1) &
(cd CatalogService && dotnet run > /tmp/catalogservice.log 2>&1) &
(cd ReservationService && dotnet run > /tmp/reservationservice.log 2>&1) &

wait