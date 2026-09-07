#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity_root="$repo_root/Nature's Bidding"
wwise_root="$unity_root/Nature's Bidding_WwiseProject"
wwise_project="$wwise_root/Nature's Bidding_WwiseProject.wproj"
output_root="$wwise_root/GeneratedSoundBanks"
source_manifest_path="$output_root/AuthoringSourceHashes.txt"
output_manifest_path="$output_root/GeneratedOutputHashes.txt"
platforms=(Mac Windows Linux)
mode="${1:-generate}"

if [[ "$mode" != "generate" && "$mode" != "--verify" ]]; then
    printf 'Usage: %s [--verify]\n' "${BASH_SOURCE[0]}" >&2
    exit 2
fi

if ! command -v perl >/dev/null 2>&1; then
    printf 'Perl is required to normalize text line endings before hashing.\n' >&2
    exit 1
fi

temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/natures-bidding-wwise.XXXXXX")"
source_paths="$temp_dir/source-paths.txt"
output_paths="$temp_dir/output-paths.txt"
before_source_manifest="$temp_dir/source-before.txt"
after_source_manifest="$temp_dir/source-after.txt"
current_output_manifest="$temp_dir/output-current.txt"
trap 'rm -f "$temp_dir"/*; rmdir "$temp_dir" 2>/dev/null || true' EXIT

hash_stdin() {
    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 | awk '{print $1}'
    else
        sha256sum | awk '{print $1}'
    fi
}

hash_file() {
    local path="$1"

    case "$path" in
        *.h|*.H|*.json|*.JSON|*.txt|*.TXT|*.xml|*.XML|*.wproj|*.WPROJ|*.wsources|*.WSOURCES|*.wwu|*.WWU)
            perl -pe 's/\r\n?/\n/g' "$path" | hash_stdin
            ;;
        *)
            if command -v shasum >/dev/null 2>&1; then
                shasum -a 256 "$path" | awk '{print $1}'
            else
                sha256sum "$path" | awk '{print $1}'
            fi
            ;;
    esac
}

write_source_manifest() {
    local destination="$1"

    (
        cd "$wwise_root"
        printf '%s\n' "$(basename "$wwise_project")"
        find . -type f \( -name '*.wwu' -o -name '*.wsources' \) -print | sed 's|^\./||'
        if [[ -d Originals ]]; then
            find Originals -type f ! -name '.*' -print
        fi
    ) | LC_ALL=C sort -u > "$source_paths"

    printf '# SHA-256 hashes for the Wwise project used to generate all SoundBanks.\n' \
        > "$destination"
    while IFS= read -r relative_path; do
        printf '%s  %s\n' "$(hash_file "$wwise_root/$relative_path")" "$relative_path" \
            >> "$destination"
    done < "$source_paths"
}

write_output_manifest() {
    local destination="$1"
    local authoring_manifest="$2"

    (
        cd "$output_root"
        find . -type f \
            ! -name '.*' \
            ! -name "$(basename "$source_manifest_path")" \
            ! -name "$(basename "$output_manifest_path")" \
            -print | sed 's|^\./||'
    ) | LC_ALL=C sort -u > "$output_paths"

    printf '# SHA-256 hashes for every generated Wwise output.\n' > "$destination"
    printf '# Authoring manifest SHA-256: %s\n' "$(hash_file "$authoring_manifest")" \
        >> "$destination"
    while IFS= read -r relative_path; do
        printf '%s  %s\n' "$(hash_file "$output_root/$relative_path")" "$relative_path" \
            >> "$destination"
    done < "$output_paths"
}

manifests_equal() {
    local recorded="$1"
    local current="$2"
    local normalized_recorded="$temp_dir/normalized-$(basename "$recorded")"

    [[ -f "$recorded" ]] || return 1
    perl -pe 's/\r\n?/\n/g' "$recorded" > "$normalized_recorded"
    cmp -s "$normalized_recorded" "$current"
}

if [[ "$mode" == "--verify" ]]; then
    write_source_manifest "$after_source_manifest"
    write_output_manifest "$current_output_manifest" "$after_source_manifest"

    if ! manifests_equal "$source_manifest_path" "$after_source_manifest"; then
        printf 'Wwise authoring data does not match %s.\n' "$source_manifest_path" >&2
        exit 1
    fi

    if ! manifests_equal "$output_manifest_path" "$current_output_manifest"; then
        printf 'Wwise generated output does not match %s.\n' "$output_manifest_path" >&2
        exit 1
    fi

    printf 'Wwise source and generated-output manifests are current.\n'
    exit 0
fi

if [[ -n "${WWISE_CONSOLE:-}" ]]; then
    wwise_console="$WWISE_CONSOLE"
elif [[ "$(uname -s)" == "Darwin" ]]; then
    wwise_app="$(xmllint --xpath \
        'string(/WwiseSettings/WwiseInstallationPathMac)' \
        "$unity_root/Assets/WwiseSettings.xml")"
    wwise_console="$wwise_app/Contents/Tools/WwiseConsole.sh"
else
    printf 'Set WWISE_CONSOLE to the full path of WwiseConsole before running this script.\n' >&2
    exit 1
fi

if [[ ! -x "$wwise_console" ]]; then
    printf 'WwiseConsole is not executable: %s\n' "$wwise_console" >&2
    exit 1
fi

write_source_manifest "$before_source_manifest"

"$wwise_console" generate-soundbank "$wwise_project" \
    --platform "${platforms[@]}" \
    --abort-on-load-issues \
    --no-source-control

for platform in "${platforms[@]}"; do
    for required_file in Init.bnk PlatformInfo.json PluginInfo.json; do
        generated_file="$output_root/$platform/$required_file"
        if [[ ! -f "$generated_file" ]]; then
            printf 'Wwise did not generate %s. The manifests were not updated.\n' \
                "$generated_file" >&2
            exit 1
        fi
    done
done

write_source_manifest "$after_source_manifest"

if ! cmp -s "$before_source_manifest" "$after_source_manifest"; then
    printf 'Wwise authoring data changed during bank generation. The manifests were not updated.\n' >&2
    exit 1
fi

write_output_manifest "$current_output_manifest" "$after_source_manifest"
write_source_manifest "$before_source_manifest"
if ! cmp -s "$before_source_manifest" "$after_source_manifest"; then
    printf 'Wwise authoring data changed while output hashes were recorded. The manifests were not updated.\n' >&2
    exit 1
fi

mv "$after_source_manifest" "$source_manifest_path"
mv "$current_output_manifest" "$output_manifest_path"
printf 'Generated all Wwise SoundBanks and updated both integrity manifests.\n'
