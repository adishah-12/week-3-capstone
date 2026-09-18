#!/bin/bash
set -e

echo "== Starting all services =="
./run-all.sh &
RUN_ALL_PID=$!

until curl -s http://localhost:5001/health > /dev/null && \
      curl -s http://localhost:5002/health > /dev/null && \
      curl -s http://localhost:5003/health > /dev/null; do
  sleep 1
done

echo "== Running smoke tests =="
./endpoint-behaviors/smoke-test.sh

echo "== Running cascade tests =="
./endpoint-behaviors/cascade-test.sh

echo "== All endpoint behavior tests completed =="

kill %1 2>/dev/null
pkill -f "dotnet.*UserService" 2>/dev/null
pkill -f "dotnet.*CatalogService" 2>/dev/null
pkill -f "dotnet.*ReservationService" 2>/dev/null
