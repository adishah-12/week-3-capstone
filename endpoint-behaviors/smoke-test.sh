#!/bin/bash
set -e

echo "== Register user =="
REGISTER=$(curl -s -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"smoke@example.com","password":"Test123!@#","firstName":"Smoke","lastName":"Test","phoneNumber":"+1-555-0000"}')
echo "$REGISTER" | jq
USER_ID=$(echo "$REGISTER" | jq -r '.userId')

echo "== Login =="
LOGIN=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"smoke@example.com","password":"Test123!@#"}')
echo "$LOGIN" | jq
TOKEN=$(echo "$LOGIN" | jq -r '.accessToken')

echo "== Validate user (internal) =="
curl -s http://localhost:5001/api/users/$USER_ID/validate | jq

echo "== Browse catalog =="
BOOKS=$(curl -s http://localhost:5002/api/catalog/books)
echo "$BOOKS" | jq
BOOK_ID=$(echo "$BOOKS" | jq -r '.content[0].bookId')

echo "== Book detail =="
curl -s http://localhost:5002/api/catalog/books/$BOOK_ID | jq

echo "== Login as librarian =="
LIBRARIAN_LOGIN=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"librarian@library.com","password":"Librarian123!"}')
echo "$LIBRARIAN_LOGIN" | jq
LIBRARIAN_TOKEN=$(echo "$LIBRARIAN_LOGIN" | jq -r '.accessToken')

echo "== Update availability (internal) =="
curl -s -X PUT http://localhost:5002/api/catalog/books/$BOOK_ID/availability \
  -H "Content-Type: application/json" \
  -d '{"delta": -1}' | jq

echo "== Create reservation =="
RESERVE=$(curl -s -X POST http://localhost:5003/api/reservations \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"bookId\":\"$BOOK_ID\"}")
echo "$RESERVE" | jq

echo "== View active reservations =="
curl -s http://localhost:5003/api/reservations \
  -H "Authorization: Bearer $TOKEN" | jq

echo "== Verify availability decremented =="
curl -s http://localhost:5002/api/catalog/books/$BOOK_ID | jq

echo "== Checkout (as patron - should 403) =="
curl -s -X POST http://localhost:5003/api/reservations/$(echo "$RESERVE" | jq -r '.reservationId')/checkout \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"notes":"test"}' | jq

echo "== Checkout (as librarian - should succeed) =="
curl -s -X POST http://localhost:5003/api/reservations/$(echo "$RESERVE" | jq -r '.reservationId')/checkout \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $LIBRARIAN_TOKEN" \
  -d '{"notes":"Book condition: Good"}' | jq

echo "== Checkout again (should 400 - already checked out) =="
curl -s -X POST http://localhost:5003/api/reservations/$(echo "$RESERVE" | jq -r '.reservationId')/checkout \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $LIBRARIAN_TOKEN" \
  -d '{"notes":"test"}' | jq

echo "== Checkout (as patron - should 403) =="
curl -s -o /dev/null -w "Status: %{http_code}\n" -X POST http://localhost:5003/api/reservations/$(echo "$RESERVE" | jq -r '.reservationId')/checkout \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"notes":"test"}'

echo "== Return (as patron - should 403) =="
curl -s -o /dev/null -w "Status: %{http_code}\n" -X POST http://localhost:5003/api/reservations/$(echo "$RESERVE" | jq -r '.reservationId')/return \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"condition":"Good"}'

echo "== Return (as librarian, no waitlist - should increment availability) =="
RETURN=$(curl -s -X POST http://localhost:5003/api/reservations/$(echo "$RESERVE" | jq -r '.reservationId')/return \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $LIBRARIAN_TOKEN" \
  -d '{"condition":"Good","notes":"Returned in good condition"}')
echo "$RETURN" | jq

echo "== Verify availability incremented back =="
curl -s http://localhost:5002/api/catalog/books/$BOOK_ID | jq

echo "== Return again (should 400 - not checked out) =="
curl -s -X POST http://localhost:5003/api/reservations/$(echo "$RESERVE" | jq -r '.reservationId')/return \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $LIBRARIAN_TOKEN" \
  -d '{"condition":"Good"}' | jq

echo "== Return with invalid condition (should 400) =="
curl -s -X POST http://localhost:5003/api/reservations/$(echo "$RESERVE" | jq -r '.reservationId')/return \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $LIBRARIAN_TOKEN" \
  -d '{"condition":"Excellent"}' | jq

echo "== Borrowing history =="
curl -s http://localhost:5003/api/reservations/history \
  -H "Authorization: Bearer $TOKEN" | jq

echo "User ID:  $USER_ID"
echo "Book ID:  $BOOK_ID"
echo "Token:    $TOKEN"