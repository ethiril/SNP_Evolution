#!/bin/sh
# Builds SNP_Evolution and installs it as the `snp-evolution` command. Run it again after changing the code.
set -e

cd "$(dirname "$0")/SNP_Evolution"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "dotnet was not found. Install the .NET 10 SDK first, e.g. 'brew install dotnet'." >&2
    exit 1
fi

# A fresh version each time, so neither dotnet tool nor the NuGet cache reuses an older build.
version="1.0.0-local.$(date +%Y%m%d%H%M%S)"

echo "Building SNP_Evolution $version..."
rm -rf nupkg
dotnet pack Cli -c Release -p:Version="$version" --nologo -v quiet

if dotnet tool list --global | grep -qi '^snp_evolution '; then
    dotnet tool uninstall --global SNP_Evolution >/dev/null
fi
dotnet tool install --global --add-source ./nupkg --version "$version" SNP_Evolution

tools_dir="$HOME/.dotnet/tools"
case ":$PATH:" in
    *":$tools_dir:"*)
        echo "Done. Run 'snp-evolution' from any folder."
        ;;
    *)
        if ! grep -qs '.dotnet/tools' "$HOME/.zshrc"; then
            printf '\n# .NET global tools\nexport PATH="$PATH:$HOME/.dotnet/tools"\n' >> "$HOME/.zshrc"
            echo "Added $tools_dir to your PATH in ~/.zshrc."
        fi
        echo "Done. Open a new terminal window, then run 'snp-evolution' from any folder."
        ;;
esac
