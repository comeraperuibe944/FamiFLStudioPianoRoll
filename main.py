import os
import subprocess
import urllib.request
import sys

def check_dotnet():
    try:
        result = subprocess.run(["dotnet", "--version"], capture_output=True, text=True, check=True)
        version = result.stdout.strip()
        print(f"Found dotnet version: {version}")
        if version.startswith("8."):
            return True
        else:
            print("Warning: .NET 8.0 is recommended, but found " + version)
            return True # might still work or they have a newer version
    except (subprocess.CalledProcessError, FileNotFoundError):
        return False

def install_dotnet():
    print(".NET CLI not found. Downloading dotnet-install.ps1...")
    url = "https://dot.net/v1/dotnet-install.ps1"
    script_path = "dotnet-install.ps1"
    urllib.request.urlretrieve(url, script_path)
    
    print("Installing .NET 8.0 SDK...")
    # Run powershell script
    subprocess.run(["powershell", "-ExecutionPolicy", "Bypass", "-File", script_path, "-Channel", "8.0"], check=True)
    print(".NET 8.0 SDK installed successfully in local user path.")
    
    # We might need to update PATH for the current process
    local_app_data = os.environ.get('LOCALAPPDATA', '')
    dotnet_path = os.path.join(local_app_data, 'Microsoft', 'dotnet')
    os.environ["PATH"] += os.pathsep + dotnet_path

def build_famistudio():
    print("Building FamiStudio...")
    sln_path = "FamiStudio.sln"
    if not os.path.exists(sln_path):
        print(f"Error: {sln_path} not found in current directory.")
        sys.exit(1)
        
    try:
        subprocess.run(["dotnet", "build", sln_path, "-c", "Release"], check=True)
        print("Build completed successfully!")
    except subprocess.CalledProcessError as e:
        print(f"Build failed with exit code {e.returncode}")
        sys.exit(1)

if __name__ == "__main__":
    if not check_dotnet():
        install_dotnet()
    build_famistudio()
