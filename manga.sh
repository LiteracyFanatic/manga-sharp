#!/bin/sh
export ASPNETCORE_ENVIRONMENT=Production
cd /usr/lib/manga/ || { echo '/usr/lib/manga/ does not exist.' 1>&2; exit 1; }
exec ./manga "$@"
