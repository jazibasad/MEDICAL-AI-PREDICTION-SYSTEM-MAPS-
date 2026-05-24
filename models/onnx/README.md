# MAPS — ONNX Model Files

Place the following pre-trained ONNX model files in this directory:

| File | Disease | Architecture | Input Shape |
|------|---------|-------------|-------------|
| `pneumonia_resnet.onnx` | Pneumonia (Chest X-Ray) | ResNet-50 | [1, 3, 224, 224] |
| `brain_tumour_densenet.onnx` | Brain Tumour (MRI) | DenseNet-121 | [1, 3, 224, 224] |
| `skin_cancer_model.onnx` | Skin Cancer (Lesion) | EfficientNet-B0 | [1, 3, 224, 224] |

## How to obtain models

These models are pre-trained in Python (PyTorch/TensorFlow) and exported to ONNX format.

### Option 1 — Use public datasets + train yourself
- Pneumonia: https://www.kaggle.com/datasets/paultimothymooney/chest-xray-pneumonia
- Brain Tumour: https://www.kaggle.com/datasets/sartajbhuvaji/brain-tumor-classification-mri
- Skin Cancer: https://www.kaggle.com/datasets/kmader/skin-lesion-analysis-toward-melanoma-detection

### Option 2 — Export from existing PyTorch model
```python
import torch
model = torch.load('your_model.pth')
model.eval()
dummy = torch.randn(1, 3, 224, 224)
torch.onnx.export(model, dummy, 'pneumonia_resnet.onnx',
                  input_names=['input'], output_names=['output'],
                  opset_version=12)
```

### Option 3 — Use ONNX Model Zoo
Pre-trained ResNet/DenseNet available at:
https://github.com/onnx/models

## Notes
- Models are mounted as read-only volumes in Docker: `onnx_models (ro)`
- In Kubernetes: shared PVC `onnx-models` mounted to all API pods
- MAPS.ML loads models as Singletons — loaded once on startup
- If model file is missing, MAPS returns a "Model Unavailable" stub result
