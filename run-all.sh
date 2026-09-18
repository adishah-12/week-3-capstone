#!/bin/bash
trap 'kill 0' EXIT

(cd UserService && dotnet run) &
(cd CatalogService && dotnet run) &
(cd ReservationService && dotnet run) &

wait