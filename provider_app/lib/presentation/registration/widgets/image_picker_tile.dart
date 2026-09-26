import 'dart:io';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

enum ImageTileShape { circle, square }

class ImagePickerTile extends StatelessWidget {
  const ImagePickerTile({
    super.key,
    required this.label,
    required this.file,
    required this.shape,
    required this.onPicked,
  });

  final String label;
  final File? file;
  final ImageTileShape shape;
  final ValueChanged<File> onPicked;

  Future<void> _pick(BuildContext context) async {
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(16))),
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.photo_camera_outlined),
              title: const Text('Take a photo'),
              onTap: () => Navigator.of(sheetContext).pop(ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library_outlined),
              title: const Text('Choose from gallery'),
              onTap: () => Navigator.of(sheetContext).pop(ImageSource.gallery),
            ),
          ],
        ),
      ),
    );

    if (source == null) return;

    final picked = await ImagePicker().pickImage(source: source, imageQuality: 85);
    if (picked != null) onPicked(File(picked.path));
  }

  @override
  Widget build(BuildContext context) {
    final isCircle = shape == ImageTileShape.circle;

    return Column(
      children: [
        GestureDetector(
          onTap: () => _pick(context),
          child: Container(
            width: 84,
            height: 84,
            decoration: BoxDecoration(
              shape: isCircle ? BoxShape.circle : BoxShape.rectangle,
              borderRadius: isCircle ? null : BorderRadius.circular(10),
              color: const Color(0xFFF1F1F7),
              border: Border.all(color: const Color(0xFFD9DBE3)),
              image: file != null
                  ? DecorationImage(image: FileImage(file!), fit: BoxFit.cover)
                  : null,
            ),
            child: file == null
                ? const Icon(Icons.add_a_photo_outlined, color: Colors.grey)
                : null,
          ),
        ),
        const SizedBox(height: 6),
        Text(label, style: const TextStyle(fontSize: 12), textAlign: TextAlign.center),
      ],
    );
  }
}
