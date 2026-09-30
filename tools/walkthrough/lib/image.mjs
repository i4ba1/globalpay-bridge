/**
 * Minimal image helpers so GIF encoding needs no native dependencies.
 */

/**
 * Area-average (box filter) downscale of an RGBA buffer. Works for any scale ratio.
 */
export function resizeRgba(src, srcWidth, srcHeight, dstWidth, dstHeight) {
  const dst = new Uint8Array(dstWidth * dstHeight * 4);
  const xRatio = srcWidth / dstWidth;
  const yRatio = srcHeight / dstHeight;

  for (let y = 0; y < dstHeight; y++) {
    const sy0 = Math.floor(y * yRatio);
    const sy1 = Math.min(srcHeight, Math.max(sy0 + 1, Math.ceil((y + 1) * yRatio)));

    for (let x = 0; x < dstWidth; x++) {
      const sx0 = Math.floor(x * xRatio);
      const sx1 = Math.min(srcWidth, Math.max(sx0 + 1, Math.ceil((x + 1) * xRatio)));

      let r = 0;
      let g = 0;
      let b = 0;
      let a = 0;
      let n = 0;

      for (let sy = sy0; sy < sy1; sy++) {
        const rowOffset = sy * srcWidth;

        for (let sx = sx0; sx < sx1; sx++) {
          const i = (rowOffset + sx) * 4;
          r += src[i];
          g += src[i + 1];
          b += src[i + 2];
          a += src[i + 3];
          n++;
        }
      }

      const o = (y * dstWidth + x) * 4;
      dst[o] = Math.round(r / n);
      dst[o + 1] = Math.round(g / n);
      dst[o + 2] = Math.round(b / n);
      dst[o + 3] = Math.round(a / n);
    }
  }

  return dst;
}
