#!/bin/bash
set -e

rm -f /tmp/service-pids.txt

echo "== Starting all services =="
./run-all.sh &

TIMEOUT=60
ELAPSED=0
until curl -s http://localhost:5001/health > /dev/null && \
      curl -s http://localhost:5002/health > /dev/null && \
      curl -s http://localhost:5003/health > /dev/null; do
  sleep 1
  ELAPSED=$((ELAPSED + 1))
  if [ "$ELAPSED" -ge "$TIMEOUT" ]; then
    echo "Services failed to start within ${TIMEOUT}s"
    cat /tmp/userservice.log /tmp/catalogservice.log /tmp/reservationservice.log
    exit 1
  fi
done

echo "== Running smoke tests =="
./endpoint-behaviors/smoke-test.sh

echo "== Running cascade tests =="
./endpoint-behaviors/cascade-test.sh

echo "== All endpoint behavior tests completed =="

if [ -f /tmp/service-pids.txt ]; then
  while read -r pid; do
    kill "$pid" 2>/dev/null || true
  done < /tmp/service-pids.txt
fi

exit 0