FROM mongo:4.4

RUN mkdir /config
WORKDIR /config

COPY wait-for-mongo.sh .
COPY mongo-setup.js .
COPY mongo-setup.sh .

RUN chmod +x /config/wait-for-mongo.sh
RUN chmod +x /config/mongo-setup.sh

CMD [ "bash", "-c", "/config/mongo-setup.sh"]
