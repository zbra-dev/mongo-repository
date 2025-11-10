#!/bin/sh
# wait-for-mongo.sh

set -e

host="$1"
shift

until mongosh --host "$host" --eval "print('waited for connection')"; do
    >&2 echo "Waiting for mongo $host..."
    sleep 1
done

>&2 echo "Mongo $host is up - saying hello..."

if (( mongosh --host "$host" --eval "db.hello()" | grep -E "ok: 1" > /dev/null )); then
    echo "Hello from $host"
fi
