#!/usr/bin/env bash

# Author: Andre Mata Assis
# AI DISCLOSURE: I wrote train.ps1 myself and had Gemini write this Bash equivalent

# SYNOPSIS
#     Use this to run training.
#
# DESCRIPTION
#     This takes a .yaml and a build of the game and runs training. The resulting .onnx file is placed in /results,
#     so ensure you run this from the project root, and that you have a /results directory on your machine.
#
# USAGE
#     ./tools/train.sh <Config> [RunId] [BuildPath] [NumEnvs] [TimeScale] [--no-graphics]
#
# EXAMPLE
#     ./tools/train.sh config/smoke.yaml Example1 "builds/MLClassProject.x86_64" 4 20 --no-graphics

set -e

# Default values
CONFIG=""
RUN_ID="run_$(date +'%Y%m%d_%H%M%S')"
BUILD_PATH="builds/MLClassProject.exe"
NUM_ENVS=4
TIME_SCALE=20
NO_GRAPHICS=false

# Print usage help
show_help() {
    cat << EOF
Usage: $0 <Config> [RunId] [BuildPath] [NumEnvs] [TimeScale] [--no-graphics]

Parameters:
  Config       Path to the .yaml config file (Required).
  RunId        Folder name for results. Default: run_[timestamp]
  BuildPath    Path to the executable. Default: builds/MLClassProject.exe
  NumEnvs      Number of concurrent Unity instances. Default: 4
  TimeScale    Time scale of Unity environment. Default: 20
  --no-graphics Run Unity without initializing graphics drivers.
EOF
}

# Parse positional arguments and flags
POSITIONAL_ARGS=()

while [[ $# -gt 0 ]]; do
  case $1 in
    -h|--help)
      show_help
      exit 0
      ;;
    --no-graphics)
      NO_GRAPHICS=true
      shift
      ;;
    *)
      POSITIONAL_ARGS+=("$1")
      shift
      ;;
  esac
done

# Assign positional parameters if provided
[[ -n "${POSITIONAL_ARGS[0]}" ]] && CONFIG="${POSITIONAL_ARGS[0]}"
[[ -n "${POSITIONAL_ARGS[1]}" ]] && RUN_ID="${POSITIONAL_ARGS[1]}"
[[ -n "${POSITIONAL_ARGS[2]}" ]] && BUILD_PATH="${POSITIONAL_ARGS[2]}"
[[ -n "${POSITIONAL_ARGS[3]}" ]] && NUM_ENVS="${POSITIONAL_ARGS[3]}"
[[ -n "${POSITIONAL_ARGS[4]}" ]] && TIME_SCALE="${POSITIONAL_ARGS[4]}"

# Check mandatory argument
if [[ -z "$CONFIG" ]]; then
    echo "Warning: Missing required parameter: Config" >&2
    show_help
    exit 1
fi

echo "Starting training: RunId='$RUN_ID' with config $CONFIG"
echo "    BuildPath = $BUILD_PATH"
echo "    NumEnvs = $NUM_ENVS"
echo "    TimeScale = $TIME_SCALE"
echo "    NoGraphics = $NO_GRAPHICS"

# Extra args for --no-graphics
EXTRA_ARGS=()
if [ "$NO_GRAPHICS" = true ]; then
    EXTRA_ARGS+=("--no-graphics")
fi

uv run mlagents-learn "$CONFIG" \
  --env="$BUILD_PATH" \
  --run-id="$RUN_ID" \
  --num-envs="$NUM_ENVS" \
  --time-scale="$TIME_SCALE" \
  --force \
  "${EXTRA_ARGS[@]}"