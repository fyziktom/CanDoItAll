#!/bin/sh
set -eu

umask 027

fail()
{
    printf '%s\n' "CanDoItAll install failed: $1" >&2
    exit 1
}

validate_release_id()
{
    case "$1" in
        ''|*[!A-Za-z0-9._-]*)
            fail "release id must contain only ASCII letters, digits, dots, underscores, or hyphens"
            ;;
    esac
}

artifact_root=''
install_root=''
release_id=''

while [ "$#" -gt 0 ]; do
    case "$1" in
        --artifact)
            [ "$#" -ge 2 ] || fail "--artifact requires a value"
            artifact_root=$2
            shift 2
            ;;
        --install-root)
            [ "$#" -ge 2 ] || fail "--install-root requires a value"
            install_root=$2
            shift 2
            ;;
        --release-id)
            [ "$#" -ge 2 ] || fail "--release-id requires a value"
            release_id=$2
            shift 2
            ;;
        --help)
            printf '%s\n' 'Usage: install-candoitall-web.sh --artifact DIR --install-root DIR --release-id ID'
            exit 0
            ;;
        *)
            fail "unknown argument '$1'"
            ;;
    esac
done

[ -n "$artifact_root" ] || fail "--artifact is required"
[ -n "$install_root" ] || fail "--install-root is required"
[ -n "$release_id" ] || fail "--release-id is required"
validate_release_id "$release_id"

case "$artifact_root" in
    /*) ;;
    *) fail "artifact path must be absolute" ;;
esac
case "$install_root" in
    /) fail "install root must be below the filesystem root" ;;
    /*) ;;
    *) fail "install root must be absolute" ;;
esac

install_root=${install_root%/}
artifact_root=${artifact_root%/}
case "$artifact_root/" in
    "$install_root/"*) fail "artifact path must not be inside the install root" ;;
esac

[ -d "$artifact_root" ] || fail "artifact directory does not exist"
[ -f "$artifact_root/CanDoItAll.Web.dll" ] || fail "artifact does not contain CanDoItAll.Web.dll"
[ -f "$artifact_root/runtime-support.json" ] || fail "artifact does not contain runtime-support.json"

script_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
for required_script in run-candoitall-web.sh rollback-candoitall-web.sh; do
    [ -f "$script_root/$required_script" ] || fail "installer companion '$required_script' is missing"
done

releases_root="$install_root/releases"
bin_root="$install_root/bin"
release_root="$releases_root/$release_id"
[ ! -e "$release_root" ] || fail "release '$release_id' already exists"

mkdir -p "$releases_root" "$bin_root"
chmod 0750 "$install_root" "$releases_root" "$bin_root"

staging_root="$install_root/.staging-$release_id-$$"
[ ! -e "$staging_root" ] || fail "staging path already exists"
cleanup()
{
    if [ -d "$staging_root" ]; then
        rm -rf "$staging_root"
    fi
}
trap cleanup EXIT HUP INT TERM

mkdir "$staging_root"
cp -R "$artifact_root/." "$staging_root/"
[ -f "$staging_root/CanDoItAll.Web.dll" ] || fail "staged artifact is incomplete"
[ -f "$staging_root/runtime-support.json" ] || fail "staged support manifest is missing"
chmod -R go-w "$staging_root"
mv "$staging_root" "$release_root"

for support_script in run-candoitall-web.sh rollback-candoitall-web.sh; do
    next_script="$bin_root/$support_script.next.$$"
    cp "$script_root/$support_script" "$next_script"
    chmod 0750 "$next_script"
    mv -f "$next_script" "$bin_root/$support_script"
done

active_state="$install_root/active-release"
previous_state="$install_root/previous-release"
if [ -f "$active_state" ]; then
    IFS= read -r previous_release < "$active_state"
    validate_release_id "$previous_release"
    [ -d "$releases_root/$previous_release" ] || fail "active release state names a missing release"
    previous_next="$previous_state.next.$$"
    printf '%s\n' "$previous_release" > "$previous_next"
    chmod 0640 "$previous_next"
    mv -f "$previous_next" "$previous_state"
fi

active_next="$active_state.next.$$"
printf '%s\n' "$release_id" > "$active_next"
chmod 0640 "$active_next"
mv -f "$active_next" "$active_state"

trap - EXIT HUP INT TERM
printf '%s\n' "Installed release '$release_id' under '$install_root'."
printf '%s\n' "Active launcher: $bin_root/run-candoitall-web.sh"
