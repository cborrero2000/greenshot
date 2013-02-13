﻿/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2013  Thomas Braun, Jens Klingen, Robin Krom
 * 
 * For more information see: http://getgreenshot.org/
 * The Greenshot project is hosted on Sourceforge: http://sourceforge.net/projects/greenshot/
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace GreenshotPlugin.Core {

	/// <summary>
	/// The interface for the FastBitmap
	/// </summary>
	public interface IFastBitmap : IDisposable {
		/// <summary>
		/// Get the color at x,y
		/// The returned Color object depends on the underlying pixel format
		/// </summary>
		/// <param name="x">int x</param>
		/// <param name="y">int y</param>
		/// <returns>Color</returns>
		Color GetColorAt(int x, int y);

		/// <summary>
		/// Set the color at the specified location
		/// </summary>
		/// <param name="x">int x</param>
		/// <param name="y">int y</param>
		/// <param name="color">Color</param>
		void SetColorAt(int x, int y, Color color);

		/// <summary>
		/// Get the color at x,y
		/// The returned byte[] color depends on the underlying pixel format
		/// </summary>
		/// <param name="x">int x</param>
		/// <param name="y">int y</par
		void GetColorAt(int x, int y, byte[] color);

		/// <summary>
		/// Set the color at the specified location
		/// </summary>
		/// <param name="x">int x</param>
		/// <param name="y">int y</param>
		/// <param name="color">byte[] color</param>
		void SetColorAt(int x, int y, byte[] color);

		/// <summary>
		/// Lock the bitmap
		/// </summary>
		void Lock();

		/// <summary>
		/// Unlock the bitmap
		/// </summary>
		void Unlock();

		/// <summary>
		/// Unlock the bitmap and get the underlying bitmap in one call
		/// </summary>
		/// <returns></returns>
		Bitmap UnlockAndReturnBitmap();

		/// <summary>
		/// Size of the underlying image
		/// </summary>
		Size Size {
			get;
		}

		/// <summary>
		/// Height of the underlying image
		/// </summary>
		int Height {
			get;
		}

		/// <summary>
		/// Width of the underlying image
		/// </summary>
		int Width {
			get;
		}

		/// <summary>
		/// Does the underlying image need to be disposed
		/// </summary>
		bool NeedsDispose {
			get;
			set;
		}
	}

	/// <summary>
	/// The base class for the fast bitmap implementation
	/// </summary>
	public unsafe abstract class FastBitmap : IFastBitmap {
		private static log4net.ILog LOG = log4net.LogManager.GetLogger(typeof(FastBitmap));

		protected const int AINDEX = 3;
		protected const int RINDEX = 2;
		protected const int GINDEX = 1;
		protected const int BINDEX = 0;
		/// <summary>
		/// If this is set to true, the bitmap will be disposed when disposing the IFastBitmap
		/// </summary>
		public bool NeedsDispose {
			get;
			set;
		}

		/// <summary>
		/// The bitmap for which the FastBitmap is creating access
		/// </summary>
		protected Bitmap bitmap;
		protected BitmapData bmData;
		protected int stride; /* bytes per pixel row */
		protected bool bitsLocked = false;
		protected byte* pointer;

		/// <summary>
		/// Factory for creating a FastBitmap depending on the pixelformat of the source
		/// </summary>
		/// <param name="source">Bitmap to access</param>
		/// <returns>IFastBitmap</returns>
		public static IFastBitmap Create(Bitmap source) {
			switch (source.PixelFormat) {
				case PixelFormat.Format8bppIndexed:
					return new FastChunkyBitmap(source);
				case PixelFormat.Format24bppRgb:
					return new Fast24RGBBitmap(source);
				case PixelFormat.Format32bppRgb:
					return new Fast32RGBBitmap(source);
				case PixelFormat.Format32bppArgb:
				case PixelFormat.Format32bppPArgb:
					return new Fast32ARGBBitmap(source);
				default:
					throw new NotSupportedException(string.Format("Not supported Pixelformat {0}", source.PixelFormat));
			}
		}

		/// <summary>
		/// Factory for creating a FastBitmap as a destination for the source
		/// </summary>
		/// <param name="source">Bitmap to access</param>
		/// <returns>IFastBitmap</returns>
		public static IFastBitmap CreateCloneOf(Image source, PixelFormat pixelFormat) {
			Bitmap destination = ImageHelper.CloneArea(source, Rectangle.Empty, pixelFormat);
			IFastBitmap fastBitmap = Create(destination);
			((FastBitmap)fastBitmap).NeedsDispose = true;
			return fastBitmap;
		}


		/// <summary>
		/// Factory for creating a FastBitmap as a destination
		/// </summary>
		/// <param name="newSize"></param>
		/// <param name="pixelFormat"></param>
		/// <param name="backgroundColor"></param>
		/// <returns>IFastBitmap</returns>
		public static IFastBitmap CreateEmpty(Size newSize, PixelFormat pixelFormat, Color backgroundColor) {
			Bitmap destination = ImageHelper.CreateEmpty(newSize.Width, newSize.Height, pixelFormat, backgroundColor, 96f, 96f);
			IFastBitmap fastBitmap = Create(destination);
			fastBitmap.NeedsDispose = true;
			return fastBitmap;
		}

		/// <summary>
		/// Constructor which stores the image and locks it when called
		/// </summary>
		/// <param name="bitmap"></param>
		protected FastBitmap(Bitmap bitmap) {
			this.bitmap = bitmap;
			Lock();
		}

		/// <summary>
		/// Return the size of the image
		/// </summary>
		public Size Size {
			get {
				return bitmap.Size;
			}
		}

		/// <summary>
		/// Return the width of the image
		/// </summary>
		public int Width {
			get {
				return bitmap.Width;
			}
		}

		/// <summary>
		/// Return the height of the image
		/// </summary>
		public int Height {
			get {
				return bitmap.Height;
			}
		}

		/// <summary>
		/// Returns the underlying bitmap, unlocks it and prevents that it will be disposed
		/// </summary>
		public Bitmap UnlockAndReturnBitmap() {
			if (bitsLocked) {
				LOG.Warn("Unlocking the bitmap");
				Unlock();
			}
			NeedsDispose = false;
			return bitmap;
		}

		/// <summary>
		/// Destructor
		/// </summary>
		~FastBitmap() {
			Dispose(false);
		}

		/// <summary>
		/// The public accessible Dispose
		/// Will call the GarbageCollector to SuppressFinalize, preventing being cleaned twice
		/// </summary>
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		// The bulk of the clean-up code is implemented in Dispose(bool)

		/// <summary>
		/// This Dispose is called from the Dispose and the Destructor.
		/// When disposing==true all non-managed resources should be freed too!
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing) {
			Unlock();
			if (disposing) {
				if (bitmap != null && NeedsDispose) {
					bitmap.Dispose();
				}
			}
			bitmap = null;
			bmData = null;
			pointer = null;
		}

		/// <summary>
		/// Lock the bitmap so we have direct access to the memory
		/// </summary>
		public void Lock() {
			if (Width > 0 && Height > 0 && !bitsLocked) {
				bmData = bitmap.LockBits(new Rectangle(Point.Empty, Size), ImageLockMode.ReadWrite, bitmap.PixelFormat);
				bitsLocked = true;

				IntPtr Scan0 = bmData.Scan0;
				pointer = (byte*)(void*)Scan0;
				stride = bmData.Stride;
			}
		}

		/// <summary>
		/// Unlock the System Memory
		/// </summary>
		public void Unlock() {
			if (bitsLocked) {
				bitmap.UnlockBits(bmData);
				bitsLocked = false;
			}
		}

		/// <summary>
		/// Draw the stored bitmap to the destionation bitmap at the supplied point
		/// </summary>
		/// <param name="graphics"></param>
		/// <param name="destination"></param>
		public void DrawTo(Graphics graphics, Point destination) {
			DrawTo(graphics, null, destination);
		}

		/// <summary>
		/// Draw the stored Bitmap on the Destination bitmap with the specified rectangle
		/// Be aware that the stored bitmap will be resized to the specified rectangle!!
		/// </summary>
		/// <param name="graphics"></param>
		/// <param name="destinationRect"></param>
		public void DrawTo(Graphics graphics, Rectangle destinationRect) {
			DrawTo(graphics, destinationRect, null);
		}

		/// <summary>
		/// private helper to draw the bitmap
		/// </summary>
		/// <param name="graphics"></param>
		/// <param name="destinationRect"></param>
		/// <param name="destination"></param>
		private void DrawTo(Graphics graphics, Rectangle? destinationRect, Point? destination) {
			if (destinationRect.HasValue) {
				// Does the rect have any pixels?
				if (destinationRect.Value.Height <= 0 || destinationRect.Value.Width <= 0) {
					return;
				}
			}
			// Make sure this.bitmap is unlocked, if it was locked
			bool isLocked = bitsLocked;
			if (isLocked) {
				Unlock();
			}

			if (destinationRect.HasValue) {
				graphics.DrawImage(this.bitmap, destinationRect.Value);
			} else if (destination.HasValue) {
				graphics.DrawImageUnscaled(this.bitmap, destination.Value);
			}
			// If it was locked, lock it again
			if (isLocked) {
				Lock();
			}
		}

		public abstract Color GetColorAt(int x, int y);
		public abstract void SetColorAt(int x, int y, Color color);
		public abstract void GetColorAt(int x, int y, byte[] color);
		public abstract void SetColorAt(int x, int y, byte[] color);
	}

	/// <summary>
	/// This is the implementation of the FastBitmat for the 8BPP pixelformat
	/// </summary>
	public unsafe class FastChunkyBitmap : FastBitmap {
		// Used for indexed images
		private Color[] colorEntries;
		private Dictionary<Color, byte> colorCache = new Dictionary<Color, byte>();

		public FastChunkyBitmap(Bitmap source) : base(source) {
			colorEntries = bitmap.Palette.Entries;
		}

		/// <summary>
		/// Get the color from the specified location
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns>Color</returns>
		public override Color GetColorAt(int x, int y) {
			int offset = x + (y * stride);
			byte colorIndex = pointer[offset];
			return colorEntries[colorIndex];
		}

		/// <summary>
		/// Get the color from the specified location into the specified array
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color">byte[4] as reference</param>
		public override void GetColorAt(int x, int y, byte[] color) {
			throw new NotImplementedException("No performance gain!");
		}

		/// <summary>
		/// Set the color at the specified location from the specified array
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color">byte[4] as reference</param>
		public override void SetColorAt(int x, int y, byte[] color) {
			throw new NotImplementedException("No performance gain!");
		}

		/// <summary>
		/// Get the color-index from the specified location
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns>byte with index</returns>
		public byte GetColorIndexAt(int x, int y) {
			int offset = x + (y * stride);
			return pointer[offset];
		}

		/// <summary>
		/// Set the color-index at the specified location
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color-index"></param>
		public void SetColorIndexAt(int x, int y, byte colorIndex) {
			int offset = x + (y * stride);
			pointer[offset] = colorIndex;
		}

		/// <summary>
		/// Set the supplied color at the specified location.
		/// Throws an ArgumentException if the color is not in the palette
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color">Color to set</param>
		public override void SetColorAt(int x, int y, Color color) {
			int offset = x + (y * stride);
			byte colorIndex;
			if (!colorCache.TryGetValue(color, out colorIndex)) {
				bool foundColor = false;
				for (colorIndex = 0; colorIndex < colorEntries.Length; colorIndex++) {
					if (color == colorEntries[colorIndex]) {
						colorCache.Add(color, colorIndex);
						foundColor = true;
						break;
					}
				}
				if (!foundColor) {
					throw new ArgumentException("No such color!");
				}
			}
			pointer[offset] = colorIndex;
		}
	}

	/// <summary>
	/// This is the implementation of the IFastBitmap for 24 bit images (no Alpha)
	/// </summary>
	public unsafe class Fast24RGBBitmap : FastBitmap {

		public Fast24RGBBitmap(Bitmap source) : base(source) {
		}

		/// <summary>
		/// Retrieve the color at location x,y
		/// Before the first time this is called the Lock() should be called once!
		/// </summary>
		/// <param name="x">X coordinate</param>
		/// <param name="y">Y Coordinate</param>
		/// <returns>Color</returns>
		public override Color GetColorAt(int x, int y) {
			int offset = (x * 3) + (y * stride);
			return Color.FromArgb(255, pointer[RINDEX + offset], pointer[GINDEX + offset], pointer[BINDEX + offset]);
		}

		/// <summary>
		/// Set the color at location x,y
		/// Before the first time this is called the Lock() should be called once!
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color"></param>
		public override void SetColorAt(int x, int y, Color color) {
			int offset = (x * 3) + (y * stride);
			pointer[RINDEX + offset] = color.R;
			pointer[GINDEX + offset] = color.G;
			pointer[BINDEX + offset] = color.B;
		}

		/// <summary>
		/// Get the color from the specified location into the specified array
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color">byte[4] as reference (a,r,g,b)</param>
		public override void GetColorAt(int x, int y, byte[] color) {
			int offset = (x * 3) + (y * stride);
			color[0] = 255;
			color[1] = pointer[RINDEX + offset];
			color[2] = pointer[GINDEX + offset];
			color[3] = pointer[BINDEX + offset];
		}

		/// <summary>
		/// Set the color at the specified location from the specified array
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color">byte[4] as reference (a,r,g,b)</param>
		public override void SetColorAt(int x, int y, byte[] color) {
			int offset = (x * 3) + (y * stride);
			pointer[RINDEX + offset] = color[1];
			pointer[GINDEX + offset] = color[2];
			pointer[BINDEX + offset] = color[3];
		}

	}

	/// <summary>
	/// This is the implementation of the IFastBitmap for 32 bit images (no Alpha)
	/// </summary>
	public unsafe class Fast32RGBBitmap : FastBitmap {
		public Fast32RGBBitmap(Bitmap source) : base(source) {

		}

		/// <summary>
		/// Retrieve the color at location x,y
		/// Before the first time this is called the Lock() should be called once!
		/// </summary>
		/// <param name="x">X coordinate</param>
		/// <param name="y">Y Coordinate</param>
		/// <returns>Color</returns>
		public override Color GetColorAt(int x, int y) {
			int offset = (x * 4) + (y * stride);
			return Color.FromArgb(255, pointer[RINDEX + offset], pointer[GINDEX + offset], pointer[BINDEX + offset]);
		}

		/// <summary>
		/// Set the color at location x,y
		/// Before the first time this is called the Lock() should be called once!
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color"></param>
		public override void SetColorAt(int x, int y, Color color) {
			int offset = (x * 4) + (y * stride);
			pointer[RINDEX + offset] = color.R;
			pointer[GINDEX + offset] = color.G;
			pointer[BINDEX + offset] = color.B;
		}

		/// <summary>
		/// Get the color from the specified location into the specified array
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color">byte[4] as reference (a,r,g,b)</param>
		public override void GetColorAt(int x, int y, byte[] color) {
			int offset = (x * 4) + (y * stride);
			color[0] = 255;
			color[1] = pointer[RINDEX + offset];
			color[2] = pointer[GINDEX + offset];
			color[3] = pointer[BINDEX + offset];
		}

		/// <summary>
		/// Set the color at the specified location from the specified array
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color">byte[4] as reference (a,r,g,b)</param>
		public override void SetColorAt(int x, int y, byte[] color) {
			int offset = (x * 4) + (y * stride);
			pointer[RINDEX + offset] = color[1];	// R
			pointer[GINDEX + offset] = color[2];
			pointer[BINDEX + offset] = color[3];
		}
	}

	/// <summary>
	/// This is the implementation of the IFastBitmap for 32 bit images with Alpha
	/// </summary>
	public unsafe class Fast32ARGBBitmap : FastBitmap {
		public Color BackgroundBlendColor {
			get;
			set;
		}
		public Fast32ARGBBitmap(Bitmap source) : base(source) {
			BackgroundBlendColor = Color.White;
		}

		/// <summary>
		/// Retrieve the color at location x,y
		/// </summary>
		/// <param name="x">X coordinate</param>
		/// <param name="y">Y Coordinate</param>
		/// <returns>Color</returns>
		public override Color GetColorAt(int x, int y) {
			int offset = (x * 4) + (y * stride);
			return Color.FromArgb(pointer[AINDEX + offset], pointer[RINDEX + offset], pointer[GINDEX + offset], pointer[BINDEX + offset]);
		}

		/// <summary>
		/// Set the color at location x,y
		/// Before the first time this is called the Lock() should be called once!
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color"></param>
		public override void SetColorAt(int x, int y, Color color) {
			int offset = (x * 4) + (y * stride);
			pointer[AINDEX + offset] = color.A;
			pointer[RINDEX + offset] = color.R;
			pointer[GINDEX + offset] = color.G;
			pointer[BINDEX + offset] = color.B;
		}

		/// <summary>
		/// Get the color from the specified location into the specified array
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color">byte[4] as reference (a,r,g,b)</param>
		public override void GetColorAt(int x, int y, byte[] color) {
			int offset = (x * 4) + (y * stride);
			color[0] = pointer[AINDEX + offset];
			color[1] = pointer[RINDEX + offset];
			color[2] = pointer[GINDEX + offset];
			color[3] = pointer[BINDEX + offset];
		}

		/// <summary>
		/// Set the color at the specified location from the specified array
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color">byte[4] as reference (a,r,g,b)</param>
		public override void SetColorAt(int x, int y, byte[] color) {
			int offset = (x * 4) + (y * stride);
			pointer[AINDEX + offset] = color[0];
			pointer[RINDEX + offset] = color[1];	// R
			pointer[GINDEX + offset] = color[2];
			pointer[BINDEX + offset] = color[3];
		}

		/// <summary>
		/// Retrieve the color, without alpha (is blended), at location x,y
		/// Before the first time this is called the Lock() should be called once!
		/// </summary>
		/// <param name="x">X coordinate</param>
		/// <param name="y">Y Coordinate</param>
		/// <returns>Color</returns>
		public Color GetBlendedColorAt(int x, int y) {
			int offset = (x * 4) + (y * stride);
			int a = pointer[AINDEX + offset];
			int red = pointer[RINDEX + offset];
			int green = pointer[GINDEX + offset];
			int blue = pointer[BINDEX + offset];

			if (a < 255) {
				// As the request is to get without alpha, we blend.
				int rem = 255 - a;
				red = (red * a + BackgroundBlendColor.R * rem) / 255;
				green = (green * a + BackgroundBlendColor.G * rem) / 255;
				blue = (blue * a + BackgroundBlendColor.B * rem) / 255;
			}
			return Color.FromArgb(255, red, green, blue);
		}

	}
}