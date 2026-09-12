#Author: Andre Mata Assis
#!/usr/bin/env bash
set -e

CONFIG=${1:-"config/smoke.yaml"} #first argument or smoke.yaml on default
RUN_ID=${2:-"smoke_$(date +%Y%m%d_%H%M%S)"} #second argument or timestamp on default
BUILD_PATH=${3:-"builds/MLClassProject.app"} #third argument: build path
NUM_ENVS=${4:-4} #fourth argument: number of enviroments (4 is pretty fast)
USE_NO_GRAPHICS=${5:-false} #fifth argument: whether training is run with graphics on

EXTRA_ARGS=""
if [ "$USE_NO_GRAPHICS" = true ] || [ "$USE_NO_GRAPHICS" = "--no-graphics" ]; then
    EXTRA_ARGS="--no-graphics"
fi

echo "Starting training: $RUN_ID with config $CONFIG"

uv run mlagents-learn "$CONFIG" \
  --env="$BUILD_PATH" \
  --run-id="$RUN_ID" \
  --num-envs="$NUM_ENVS" \
  --time-scale=20 \
  --force \
  $EXTRA_ARGS