#!/bin/sh
set -eu

umask 027

fail()
{
    printf '%s\n' "CanDoItAll rollback failed: $1" >&2
    exit 1
}

validate_release_id()
{
    case "$1" in
        ''|*[!A-Za-z0-9._-]*) fail "release state is invalid" ;;
    esac
}

[ "$#" -eq 1 ] || fail "usage: rollback-candoitall-web.sh INSTALL_ROOT"
install_root=${1%/}
case "$install_root" in
    /) fail "install root must be below the filesystem root" ;;
    /*) ;;
    *) fail "install root must be absolute" ;;
esac

active_state="$install_root/active-release"
previous_state="$install_root/previous-release"
[ -f "$active_state" ] || fail "active release state is missing"
[ -f "$previous_state" ] || fail "no previous release is available"
IFS= read -r active_release < "$active_state"
IFS= read -r previous_release < "$previous_state"
validate_release_id "$active_release"
validate_release_id "$previous_release"
[ -d "$install_root/releases/$previous_release" ] || fail "previous release directory is missing"

if [ "$active_release" = "$previous_release" ]; then
    printf '%s\n' "Release '$previous_release' is already active."
    exit 0
fi

rollback_from_next="$install_root/rollback-from.next.$$"
printf '%s\n' "$active_release" > "$rollback_from_next"
chmod 0640 "$rollback_from_next"
mv -f "$rollback_from_next" "$install_root/rollback-from"

active_next="$active_state.next.$$"
printf '%s\n' "$previous_release" > "$active_next"
chmod 0640 "$active_next"
mv -f "$active_next" "$active_state"

printf '%s\n' "Activated previous release '$previous_release'."
printf '%s\n' 'Restart the service and verify /health before removing any release.'
