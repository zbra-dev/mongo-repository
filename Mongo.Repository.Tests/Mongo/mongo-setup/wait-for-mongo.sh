#!/bin/sh
# wait-for-mongo.sh

set -e

host="$1"
shift

until mongo --host "$host" --eval "print('waited for connection')"; do
    >&2 echo "Waiting for mongo $host..."
    sleep 1
done

>&2 echo "Mongo $host is up - executing command"
exec "$@"
