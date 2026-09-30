import fs from 'node:fs';
import { PNG } from 'pngjs';
// gifenc's package entry is CommonJS, which Node cannot expose as named ESM exports; import the ESM build directly.
import { GIFEncoder, quantize, applyPalette } from 'gifenc/dist/gifenc.esm.js';
import { resizeRgba } from './image.mjs';

/**
 * Encodes a sequence of PNG screenshots into one animated GIF.
 * Every frame must share the same pixel dimensions (use a fixed clip per step).
 */
export function encodeGif(pngBuffers, outPath, options = {}) {
  const {
    targetWidth = 900,
    delayMs = 700,
    firstDelayMs = 1000,
    maxColors = 256,
  } = options;

  if (!pngBuffers.length) {
    throw new Error('encodeGif requires at least one frame');
  }

  const decoded = pngBuffers.map((buffer) => PNG.sync.read(buffer));
  const { width: srcWidth, height: srcHeight } = decoded[0];

  for (const [index, png] of decoded.entries()) {
    if (png.width !== srcWidth || png.height !== srcHeight) {
      throw new Error(
        `frame ${index} is ${png.width}x${png.height} but frame 0 is ${srcWidth}x${srcHeight}; ` +
          'use a fixed clip per step',
      );
    }
  }

  const dstWidth = Math.min(targetWidth, srcWidth);
  const dstHeight = Math.round(srcHeight * (dstWidth / srcWidth));

  const gif = GIFEncoder();

  decoded.forEach((png, index) => {
    const rgba = new Uint8Array(png.data.buffer, png.data.byteOffset, png.data.length);
    const frame = dstWidth === srcWidth ? rgba : resizeRgba(rgba, srcWidth, srcHeight, dstWidth, dstHeight);
    const palette = quantize(frame, maxColors);
    const indexed = applyPalette(frame, palette);

    gif.writeFrame(indexed, dstWidth, dstHeight, {
      palette,
      delay: index === 0 ? firstDelayMs : delayMs,
    });
  });

  gif.finish();
  fs.writeFileSync(outPath, gif.bytes());

  return {
    width: dstWidth,
    height: dstHeight,
    frames: decoded.length,
    bytes: fs.statSync(outPath).size,
  };
}
