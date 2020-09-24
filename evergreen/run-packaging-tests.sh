#!/bin/bash
#set -o xtrace   # Write all commands first to stderr
set -o errexit  # Exit the script with error if any of the commands fail

# Colors
NC='\033[0m'
RED='\033[0;31m'
GREEN='\033[0;32m'
ORANGE='\033[0;33m'
PURPLE='\033[0;35m'
DARK_GREY='\033[1;30m'

# Constants
mongocrypt_version="1.0.4" # This is the hardcoded version returned by mongocrypt.dll. If mongocrypt.dll is ever updated, the value here must be updated as well
project_directory_name="Packaging"
nuget_source_name="Nuget Local Source"
nuget_source_directory_name="NugetLocalSource"
nuget_source_path=$(cygpath -w $(realpath ./$nuget_source_directory_name))

work_directory=$PWD

# Command line argument variables
cleanup_folders=true # Note: local nuget source is disposed regardless of this flag, because local nuget sources are evil
run_tests=true
run_build=true
libmongocrypt_project_path="C:/Projects/MongoDb/libmongocrypt/bindings/cs/MongoDB.Libmongocrypt/"
driver_project_path="C:/Projects/MongoDb/mongo-csharp-driver/src/MongoDB.Driver/"
bson_project_path="C:/Projects/MongoDb/mongo-csharp-driver/src/MongoDB.Bson/"
driver_core_project_path="C:/Projects/MongoDb/mongo-csharp-driver/src/MongoDB.Driver.Core/"

# Process command line arguments
POSITIONAL=()
while [[ $# -gt 0 ]]; do
    key="$1"
    case $key in
        -nc|--no-cleanup)
        cleanup_folders=false
        shift
        ;;
        -nt|--no-test)
        run_tests=false
        shift
        ;;
        -nb|--no-build)
        run_build=false
        shift
        ;;
        -lp|--libmongocrypt-path)
        libmongocrypt_project_path="$2"
        shift
        shift
        ;;
        -dp|--driver-path)
        driver_project_path="$2"
        shift
        shift
        ;;
        -bp|--bson-path)
        bson_project_path="$2"
        shift
        shift
        ;;
        -cp|--core-path)
        driver_core_project_path="$2"
        shift
        shift
        ;;
        *)
        echo "invalid argument $1"
        exit 1
        ;;
    esac
done
set -- "${POSITIONAL[@]}" # Restore positional parameters

function setup_local_nuget_source {
    rm -rf "$nuget_source_path"
    mkdir "$nuget_source_directory_name"
    printf "Start local nuget source setup..."
    dotnet nuget locals all --clear &> /dev/null
    dotnet nuget add source "$nuget_source_path" -n "$nuget_source_name" > /dev/null
    printf "done.\n"
}

function cleanup_local_nuget_source {
    dotnet nuget remove source "$nuget_source_name" &> /dev/null # Remove local nuget source
}

function cleanup {
    printf "Cleanup started..."
    pushd "$driver_project_path" > /dev/null
    dotnet add package MongoDB.Libmongocrypt > /dev/null # Restore Driver project
    popd > /dev/null
    pushd "$driver_core_project_path" > /dev/null
    dotnet add package MongoDB.Libmongocrypt > /dev/null # Restore Driver.Core project
    popd > /dev/null
    if [ "$cleanup_folders" = true ]; then
        rm -rf "$nuget_source_path"
        cd $work_directory
        rm -rf "$project_directory_name"
    fi
    ! cleanup_local_nuget_source
    printf "done.\n"
}

trap cleanup EXIT

function build_local_nuget {
    project_path=$1
    project_name=$2
    nuget_version=$3
    nuget_name="$project_name.$nuget_version.nupkg"

    printf "Building $project_name package..."

    pushd "$project_path" > /dev/null
    dotnet pack "$project_name.csproj" -p:PackageVersion="$nuget_version" -p:NoWarn=NU1605 > /dev/null
    mv "./bin/Debug/$nuget_name" "$nuget_source_path/$nuget_name"
    popd > /dev/null

    printf "done.\n"
}

function test_packaging {

    # Configure local nuget source. Note: it must be disposed
    setup_local_nuget_source

    # Set MongoDB.Driver.Core libmongocrypt nuget version to test version
    pushd "$driver_core_project_path" > /dev/null
    dotnet add package MongoDB.Libmongocrypt --no-restore --version 0.0.0-local > /dev/null
    popd > /dev/null

    # Remove reference from MongoDB.Driver
    pushd "$driver_project_path" > /dev/null
    dotnet remove reference MongoDB.Libmongocrypt > /dev/null
    popd > /dev/null

    if [ "$run_build" = true ]; then
        # Build Libmongocrypt nuget
        build_local_nuget \
            "$libmongocrypt_project_path" \
            "MongoDB.Libmongocrypt" \
            "0.0.0-local"

        # Build Bson nuget
        build_local_nuget \
            "$bson_project_path" \
            "MongoDB.Bson" \
            "0.0.0-local"

        # Build Driver.Core nuget
        build_local_nuget \
            "$driver_core_project_path" \
            "MongoDB.Driver.Core" \
            "0.0.0-local"

        # Build Driver nuget
        build_local_nuget \
            "$driver_project_path" \
            "MongoDB.Driver" \
            "0.0.0-local"
    fi

    # Prepare projects directory
    driver_version="2.11.2"
    rm -rf $project_directory_name  # Clear projects folder
    mkdir $project_directory_name   # Create projects folder
    pushd $project_directory_name > /dev/null

    # Test frameworks
    for target_framework in netcoreapp1.0 netcoreapp2.0 netcoreapp3.0 net452 net472; do

        project_name=${target_framework^}   # Make first letter capital
        project_name=${project_name//.}     # Remove dots from framework name

        # Create SDK-style project
        dotnet new console -n $project_name --target-framework-override $target_framework > /dev/null
        pushd $project_name > /dev/null

        # Replace default code with test code
        cat > ./Program.cs <<EOF
using MongoDB.Libmongocrypt;
using System;

namespace Packaging
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(Library.Version);
        }
    }
}
EOF
        # Add test package
        dotnet add package MongoDB.Driver --no-restore --version 0.0.0-local > /dev/null

        # Build test project. Option "-p:NoWarn=NU1605" ignores nuget outdated versions check
        dotnet build -p:NoWarn=NU1605 > /dev/null

        # Validate dotnet run result
        if [ "$run_tests" = true ]; then
            run_result=$(dotnet run --no-build 2> /dev/null)
            if [ "$run_result" = "$mongocrypt_version" ]; then
                printf "${GREEN}%s${NC}\n" "SUCCESS: $target_framework passed"
            else
                printf "${RED}%s${NC}\n" "FAIL: $target_framework run returned incorrect version: $run_result"
                exit 1
            fi
        fi

        popd > /dev/null # pushd $project_name > /dev/null
    done

    popd > /dev/null # pushd $project_directory_name > /dev/null
}

test_packaging
printf "${GREEN}Test packaging completed${NC}\n"