#!/bin/sh
set -eu

fail()
{
    printf '%s\n' "CanDoItAll start failed: $1" >&2
    exit 1
}

[ "$#" -ge 1 ] || fail "install root argument is required"
install_root=${1%/}
shift
case "$install_root" in
    /) fail "install root must be below the filesystem root" ;;
    /*) ;;
    *) fail "install root must be absolute" ;;
esac

active_state="$install_root/active-release"
[ -f "$active_state" ] || fail "active release state is missing"
IFS= read -r release_id < "$active_state"
case "$release_id" in
    ''|*[!A-Za-z0-9._-]*) fail "active release state is invalid" ;;
esac

release_root="$install_root/releases/$release_id"
app_dll="$release_root/CanDoItAll.Web.dll"
[ -f "$app_dll" ] || fail "active release is incomplete"
[ -f "$release_root/runtime-support.json" ] || fail "active release support manifest is missing"

cd "$release_root"
exec dotnet "$app_dll" "$@"
