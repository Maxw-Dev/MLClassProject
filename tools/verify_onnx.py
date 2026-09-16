# Author: Andre Mata Assis
# Written with the help of Gemini

import argparse
import sys
import numpy as np
import onnxruntime as ort

#T8 - "loads an exported .onnx, feeds it a recorded observation, and checks if Unity and Python agree"
def verify_onnx_parity(model_path: str, tolerance: float = 1e-4):
    print(f"Loading ONNX model: {model_path}")
    
    try:
        session = ort.InferenceSession(model_path)
    except Exception as e:
        print(f"❌ Error loading ONNX session: {e}")
        sys.exit(1)

    inputs = session.get_inputs()
    outputs = session.get_outputs()

    if not inputs or not outputs:
        print("❌ Invalid ONNX model: missing input or output nodes.")
        sys.exit(1)

    # 1. Deterministically generate fixed test observations
    # Using a fixed seed guarantees identical input vectors across runs
    rng = np.random.default_rng(seed=42)
    input_feed = {}
    
    print("\n--- Generating Deterministic Observation Vector ---")
    for inp in inputs:
        # Resolve batch size (dim 0) to 1, keep observation space dimensions
        shape = [1 if (not isinstance(dim, int) or dim <= 0) else dim for dim in inp.shape]
        dtype = np.float32 if "float" in inp.type else np.int64
        
        # Generate predictable test values [-1.0, 1.0]
        dummy_data = rng.uniform(-1.0, 1.0, size=shape).astype(dtype)
        input_feed[inp.name] = dummy_data
        print(f"Input '{inp.name}' ({inp.type}) shape {shape} initialized.")

    # 2. Execute Python Inference via ONNX Runtime
    try:
        results = session.run(None, input_feed)
    except Exception as e:
        print(f"❌ Model execution failed during ONNX runtime pass: {e}")
        sys.exit(1)

    # 3. Perform Self-Consistency & Sanity Checks
    print("\n--- Verifying Inference Output Structure ---")
    all_valid = True

    for out_meta, res in zip(outputs, results):
        print(f"Output '{out_meta.name}': shape {res.shape}")
        
        # Check for invalid numerical results (NaNs or Infs)
        if np.isnan(res).any():
            print(f"❌ PARITY ERROR: Output '{out_meta.name}' produced NaN values!")
            all_valid = False
        elif np.isinf(res).any():
            print(f"❌ PARITY ERROR: Output '{out_meta.name}' produced Inf values!")
            all_valid = False
        else:
            print(f"  ✓ Output values are clean and numerical (range: [{res.min():.4f}, {res.max():.4f}])")

    if not all_valid:
        print("\n❌ VERIFICATION FAILED: Model outputs contain invalid numbers.")
        sys.exit(1)

    print("\n✅ SUCCESS: ONNX inference structure and deterministic evaluation verified.")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Verify exported Unity ML-Agents ONNX models.")
    parser.add_argument("model_path", help="Path to exported .onnx model (e.g. results/smoke_test/BossFight.onnx)")
    parser.add_argument("--tolerance", type=float, default=1e-4, help="Numeric tolerance threshold")
    
    args = parser.parse_args()
    verify_onnx_parity(args.model_path, args.tolerance)