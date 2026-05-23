#!/bin/sh
# docker-entrypoint.sh — substitutes ${API_UPSTREAM} in the nginx template and
# then starts nginx.  Only ${API_UPSTREAM} is substituted; all other nginx
# variables ($host, $uri, $remote_addr …) are left untouched.
set -e

envsubst '${API_UPSTREAM}' \
    < /etc/nginx/templates/default.conf.template \
    > /etc/nginx/conf.d/default.conf

exec nginx -g 'daemon off;'