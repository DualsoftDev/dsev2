#!/bin/bash

USER="ds"

# 1. 비밀번호 파일 경로
PASSFILE=".pgpass.secret"

# 2. 비밀번호 로드 또는 입력
if [ -f "$PASSFILE" ]; then
  PGPASSWORD=$(<"$PASSFILE")
else
  read -s -p "Enter postgres password: " PGPASSWORD
  echo ""
  echo "$PGPASSWORD" > "$PASSFILE"
  chmod 600 "$PASSFILE"  # 보안: 파일 권한 설정
fi
export PGPASSWORD

function psql() {
  "/c/Program Files/PostgreSQL/17/bin/psql.exe" -h localhost -p 5432 "$@"
}

# PostgreSQL 명령 실행 함수
function sql() {
  psql -U postgres -h localhost -p 5432 -d "$1" -c "$2"
}

# ⬇️ 데이터베이스 존재 여부 확인 함수
function db_exists() {
  psql -U postgres -h localhost -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$USER'" | grep -q 1
}
function installtime-initialize() {
  # 1. CREATE ROLE & DATABASE
  sql postgres "CREATE ROLE $USER LOGIN PASSWORD 'ds';"     #'$USER' 로 사용할 수 없음.
  sql postgres "CREATE DATABASE $USER OWNER $USER;"
}

# ⬇️ 초기화 필요 여부 판단
if ! db_exists; then
  installtime-initialize
else
  echo "데이터베이스 '$USER'가 이미 존재합니다. 초기화 생략."
fi

# 스키마 (재)생성 및 권한 부여
#sql postgres "DROP SCHEMA $USER CASCADE;"
sql $USER "DROP SCHEMA IF EXISTS $USER CASCADE;"
# ⬇️ 'postgres' 슈퍼유저가 '$USER' 데이터베이스에 접속해서 권한 부여 및 설정
sql $USER "CREATE SCHEMA $USER AUTHORIZATION $USER;"  # 스키마 생성
sql $USER "GRANT USAGE, CREATE ON SCHEMA $USER TO $USER;"  # $USER 유저에게 권한 부여
sql $USER "ALTER ROLE $USER SET search_path = $USER, public;"  # $USER 유저 접속 시 기본 경로



echo "PostgreSQL 초기화가 완료되었습니다."
