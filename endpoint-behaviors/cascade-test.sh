#!/bin/bash
set -e

echo "== Register Patron A =="
A_REG=$(curl -s -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"patronA@example.com","password":"Test123!@#","firstName":"Patron","lastName":"A","phoneNumber":"+1-555-1111"}')
echo "$A_REG" | jq
A_TOKEN=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"patronA@example.com","password":"Test123!@#"}' | jq -r '.accessToken')

echo "== Register Patron B =="
B_REG=$(curl -s -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"patronB@example.com","password":"Test123!@#","firstName":"Patron","lastName":"B","phoneNumber":"+1-555-2222"}')
echo "$B_REG" | jq
B_TOKEN=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"patronB@example.com","password":"Test123!@#"}' | jq -r '.accessToken')

echo "== Librarian login =="
LIB_TOKEN=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"librarian@library.com","password":"Librarian123!"}' | jq -r '.accessToken')

echo "== Find a book with only 1 available copy (Clean Code: 2 available) =="
BOOKS=$(curl -s http://localhost:5002/api/catalog/books)
BOOK_ID=$(echo "$BOOKS" | jq -r '.content[] | select(.title=="Clean Code") | .bookId')
echo "Book ID: $BOOK_ID"

echo "== Drain Clean Code down to 0 availableCopies (starts at 2) =="
curl -s -X PUT http://localhost:5002/api/catalog/books/$BOOK_ID/availability \
  -H "Content-Type: application/json" -d '{"delta": -2}' | jq

echo "== Patron A tries to reserve (should fail - 0 available) =="
curl -s -X POST http://localhost:5003/api/reservations \
  -H "Content-Type: application/json" -H "Authorization: Bearer $A_TOKEN" \
  -d "{\"bookId\":\"$BOOK_ID\"}" | jq

echo "== Restore 1 copy so Patron A CAN reserve it (simulating the last copy) =="
curl -s -X PUT http://localhost:5002/api/catalog/books/$BOOK_ID/availability \
  -H "Content-Type: application/json" -d '{"delta": 1}' | jq

echo "== Patron A reserves the last copy =="
A_RESERVE=$(curl -s -X POST http://localhost:5003/api/reservations \
  -H "Content-Type: application/json" -H "Authorization: Bearer $A_TOKEN" \
  -d "{\"bookId\":\"$BOOK_ID\"}")
echo "$A_RESERVE" | jq
A_RESERVATION_ID=$(echo "$A_RESERVE" | jq -r '.reservationId')

echo "== Verify availableCopies is now 0 =="
curl -s http://localhost:5002/api/catalog/books/$BOOK_ID | jq '.availableCopies'

echo "== Librarian checks out Patron A's reservation =="
curl -s -X POST http://localhost:5003/api/reservations/$A_RESERVATION_ID/checkout \
  -H "Content-Type: application/json" -H "Authorization: Bearer $LIB_TOKEN" \
  -d '{"notes":"Good"}' | jq

echo "== Patron B joins the waitlist (should succeed - availableCopies is 0) =="
B_JOIN=$(curl -s -X POST http://localhost:5003/api/reservations/waitlist \
  -H "Content-Type: application/json" -H "Authorization: Bearer $B_TOKEN" \
  -d "{\"bookId\":\"$BOOK_ID\"}")
echo "$B_JOIN" | jq
B_WAITLIST_ID=$(echo "$B_JOIN" | jq -r '.waitlistId')

echo "== Patron B views their waitlist (should show WAITING, position 1) =="
curl -s http://localhost:5003/api/reservations/waitlist \
  -H "Authorization: Bearer $B_TOKEN" | jq

echo "== KEY TEST: Librarian returns Patron A's book =="
curl -s -X POST http://localhost:5003/api/reservations/$A_RESERVATION_ID/return \
  -H "Content-Type: application/json" -H "Authorization: Bearer $LIB_TOKEN" \
  -d '{"condition":"Good","notes":"test"}' | jq

echo "== Verify availableCopies STAYED at 0 (not incremented) =="
curl -s http://localhost:5002/api/catalog/books/$BOOK_ID | jq '.availableCopies'

echo "== Patron B's waitlist entry should now be NOTIFIED with claimDeadline =="
curl -s http://localhost:5003/api/reservations/waitlist \
  -H "Authorization: Bearer $B_TOKEN" | jq

echo "== Patron B should now have an auto-created RESERVED reservation =="
curl -s http://localhost:5003/api/reservations \
  -H "Authorization: Bearer $B_TOKEN" | jq