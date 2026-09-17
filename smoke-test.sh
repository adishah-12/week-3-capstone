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

echo "User ID:  $USER_ID"
echo "Book ID:  $BOOK_ID"
echo "Token:    $TOKEN"