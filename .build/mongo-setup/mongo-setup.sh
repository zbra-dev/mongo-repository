#!/usr/bin/env bash

/config/wait-for-mongo.sh mongodb1:27017 -- echo "mongodb1 ready"
/config/wait-for-mongo.sh mongodb2:27017 -- echo "mongodb2 ready"
/config/wait-for-mongo.sh mongodb3:27017 -- echo "mongodb3 ready"

if [ ! -f /data/mongo-init.flag ]; then
    echo "Init replicaset"
    mongosh mongodb://mongodb1:27017 mongo-setup.js
    touch /data/mongo-init.flag
else
    echo "Replicaset already initialized"
fi

echo "Checking if database is ready...."

until mongosh --host mongodb1:27017 --eval "db.hello().isWritablePrimary" | tail -c+1 | grep "true" > /dev/null; do
    echo "Waiting for Replica Set to Initialize Primary....."
    sleep 1
done

echo "Replicaset is ready"
