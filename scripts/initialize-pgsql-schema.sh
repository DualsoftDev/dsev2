#!/bin/bash

# Help function
show_help() {
  cat << EOF
PostgreSQL Database Initialization Script

Usage: $0 [OPTIONS]

OPTIONS:
  -h, --help              Show this help message and exit
  -u, --user USERNAME     PostgreSQL username (default: 'ds')
  -p, --password PASSWORD PostgreSQL password (default: same as username)

Examples:
  $0                               # use default user 'ds' and password 'ds'
  $0 -u myuser                     # use user 'myuser' and password 'myuser'
  $0 --user myuser                 # use user 'myuser' and password 'myuser'
  $0 -u myuser -p mypass           # use user 'myuser' and password 'mypass'
  $0 --user myuser --password mypass  # use user 'myuser' and password 'mypass'

Legacy positional arguments (deprecated):
  $0 [user] [password]             # positional arguments still supported

Description:
  This script initializes PostgreSQL database with the specified user.
  It creates a role, database, and schema for the user.
  The PostgreSQL admin password is read from .pgpass.secret file or prompted.
EOF
}

# Default values
USER="ds"
USER_PASSWORD=""

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    -h|--help)
      show_help
      exit 0
      ;;
    -u|--user)
      USER="$2"
      shift 2
      ;;
    -p|--password)
      USER_PASSWORD="$2"
      shift 2
      ;;
    -*)
      echo "Unknown option: $1"
      echo "Use '$0 --help' for more information."
      exit 1
      ;;
    *)
      # Handle legacy positional arguments
      if [ -z "$POSITIONAL_ARGS" ]; then
        POSITIONAL_ARGS=()
      fi
      POSITIONAL_ARGS+=("$1")
      shift
      ;;
  esac
done

# Handle legacy positional arguments if no options were used
if [ -n "$POSITIONAL_ARGS" ]; then
  if [ ${#POSITIONAL_ARGS[@]} -eq 1 ]; then
    USER="${POSITIONAL_ARGS[0]}"
  elif [ ${#POSITIONAL_ARGS[@]} -eq 2 ]; then
    USER="${POSITIONAL_ARGS[0]}"
    USER_PASSWORD="${POSITIONAL_ARGS[1]}"
  elif [ ${#POSITIONAL_ARGS[@]} -gt 2 ]; then
    echo "Too many arguments."
    echo "Use '$0 --help' for more information."
    exit 1
  fi
fi

# Set default password if not specified
if [ -z "$USER_PASSWORD" ]; then
  USER_PASSWORD="$USER"
fi

# PostgreSQL 관리자 비밀번호 파일 경로
PASSFILE=".pgpass.secret"

# PostgreSQL 관리자 비밀번호 로드 또는 입력
if [ -f "$PASSFILE" ]; then
  PGPASSWORD=$(<"$PASSFILE")
else
  read -s -p "Enter postgres admin password: " PGPASSWORD
  echo ""
  echo "$PGPASSWORD" > "$PASSFILE"
  chmod 600 "$PASSFILE"  # 보안: 파일 권한 설정
fi
export PGPASSWORD

function psql() {
  if grep -qEi "(Microsoft|WSL)" /proc/version &> /dev/null; then
    #echo "Running in WSL .alias"
    /usr/bin/psql -h localhost -p 5432 "$@"
  else
    #echo "Not in WSL, .alias"
    "/c/Program Files/PostgreSQL/17/bin/psql.exe" -h localhost -p 5432 "$@"
  fi
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
  sql postgres "CREATE ROLE $USER LOGIN PASSWORD '$USER_PASSWORD';"
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
