﻿using System;
using System.Drawing;
using System.Windows.Forms;

namespace _036circles
{
  public partial class Form1 : Form
  {
    static readonly string rev = "$Rev$".Split( ' ' )[ 1 ];

    protected Bitmap inputImage = null;
    protected Bitmap outputImage = null;

    public Form1 ()
    {
      InitializeComponent();
      Text += " (rev: " + rev + ')';
    }

    private void setImage ( ref Bitmap bakImage, Bitmap newImage )
    {
      pictureBox1.Image = newImage;
      if ( bakImage != null )
        bakImage.Dispose();
      bakImage = newImage;
    }

    private void buttonLoadImage_Click ( object sender, EventArgs e )
    {
      OpenFileDialog ofd = new OpenFileDialog();

      ofd.Title = "Open Image File";
      ofd.Filter = "Bitmap Files|*.bmp" +
          "|Gif Files|*.gif" +
          "|JPEG Files|*.jpg" +
          "|PNG Files|*.png" +
          "|TIFF Files|*.tif" +
          "|All image types|*.bmp;*.gif;*.jpg;*.png;*.tif";

      ofd.FilterIndex = 6;
      ofd.FileName = "";
      if ( ofd.ShowDialog() != DialogResult.OK )
        return;

      setImage( ref inputImage, (Bitmap)Image.FromFile( ofd.FileName ) );

      string txt = string.Format( "Input: {0} ({1}x{2}, {3})",
                                  ofd.FileName, inputImage.Width, inputImage.Height, inputImage.PixelFormat.ToString() );
      labelImageName.Text = txt;
    }

    private void buttonRedraw_Click ( object sender, EventArgs e )
    {
      int width    = (int)numericXres.Value;
      int height   = (int)numericYres.Value;

      Canvas c = new Canvas( width, height );

      Circles.Draw( c, inputImage, textParam.Text );

      setImage( ref outputImage, c.Finish() );

      buttonSave.Enabled = true;
    }

    private void buttonSave_Click ( object sender, EventArgs e )
    {
      if ( outputImage == null ) return;

      SaveFileDialog sfd = new SaveFileDialog();
      sfd.Title = "Save PNG file";
      sfd.Filter = "PNG Files|*.png";
      sfd.AddExtension = true;
      sfd.FileName = "";
      if ( sfd.ShowDialog() != DialogResult.OK )
        return;

      outputImage.Save( sfd.FileName, System.Drawing.Imaging.ImageFormat.Png );
    }
  }
}
