#!/bin/sh
# Installs Claude Usage for the current user, so it shows up in the application menu with its icon.
# Nothing goes outside the home folder and nothing needs root.
#
#   ~/.local/share/ClaudeUsage/ClaudeUsage                      the program
#   ~/.local/share/icons/hicolor/<size>/apps/claude-usage.png   the icon, one per size in icons/
#   ~/.local/share/icons/hicolor/scalable/apps/claude-usage.svg
#   ~/.local/share/applications/claude-usage.desktop            the menu entry
#
#   ./install.sh              install the ClaudeUsage program that lies next to this script
#   ./install.sh PROGRAM      install the given ClaudeUsage program file
#   ./install.sh --uninstall  remove it all again
set -e

here=$(cd "$(dirname "$0")" && pwd)
data="${XDG_DATA_HOME:-$HOME/.local/share}"
app="$data/ClaudeUsage"
icons="$data/icons/hicolor"
menu="$data/applications"

if [ "$1" = "--uninstall" ]; then
    rm -rf "$app"
    rm -f "$menu/claude-usage.desktop"
    find "$icons" -name 'claude-usage.*' -path '*/apps/*' -delete 2>/dev/null || true
    echo "Claude Usage removed."
    exit 0
fi

program="${1:-$here/ClaudeUsage}"
if [ ! -f "$program" ]; then
    echo "Program file not found: $program" >&2
    echo "Usage: $0 [PROGRAM]" >&2
    exit 1
fi

mkdir -p "$app" "$menu"
cp "$program" "$app/ClaudeUsage"
chmod +x "$app/ClaudeUsage"

# icons/<size>/claude-usage.png and icons/scalable/claude-usage.svg.
for dir in "$here"/icons/*/; do
    size=$(basename "$dir")
    mkdir -p "$icons/$size/apps"
    cp "$dir"claude-usage.* "$icons/$size/apps/"
done

# The menu entry needs the full path of the program, which is only known now.
sed "s|__EXEC__|\"$app/ClaudeUsage\"|" "$here/claude-usage.desktop" > "$menu/claude-usage.desktop"
chmod +x "$menu/claude-usage.desktop"

# Both refreshes are optional; most desktops notice the new files on their own.
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$menu" || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q -t "$icons" || true

echo "Claude Usage installed. Start it from the application menu, or run $app/ClaudeUsage"
