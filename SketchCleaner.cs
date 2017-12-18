using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Drawing.Imaging;

namespace Images
{
    public partial class SketchCleaner : Form
    {
        public SketchCleaner()
        {
            InitializeComponent();
        }

        private string BaseDir = "C:\\Users\\Mehdy Faik\\Desktop\\Work\\Work\\Side Hustles\\Images\\Images\\Test Images\\";
        private int BitmapHeaderLength = 54;

        #region Load/Save.
        private void loadImage_btn_Click(object sender, EventArgs e)
        {
            //SaturateBitmap(new Bitmap("C:\\Users\\Mehdy Faik\\Desktop\\Work\\Work\\Side Hustles\\Images\\Images\\Test Images\\IMG_0498.bmp"));

            //Offer dialog.
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = BaseDir;
            Stream inputFileStream;
            Bitmap toPictureBox = new Bitmap(100, 100);

            //Load file.
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if ((inputFileStream = ofd.OpenFile()) != null)
                    {
                        using (inputFileStream)
                        {
                            toPictureBox = new Bitmap(inputFileStream);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Invalid file - please load .bmp only");
                }
            }

            //Show it.
            mainPicture_pb.Image = toPictureBox;
        }

        private void savePicturebox_btn_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.DefaultExt = ".bmp";
            Bitmap fromPictureBox;
            BitmapToBitmap_24bppRgb((Bitmap)mainPicture_pb.Image, out fromPictureBox);

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                fromPictureBox.Save(sfd.FileName);
            }
        }
        #endregion

        private void saturateColors_btn_Click(object sender, EventArgs e)
        {
            //Load location.
            string loadPath = "";
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select File Load Path";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                loadPath = ofd.FileName;
            }

            //BackgroundWorker.
            BackgroundWorker bgw = new BackgroundWorker();
            
            bgw.WorkerReportsProgress = true;
            
            bgw.DoWork += BackgroundWorker_SingleUse_SaturateBitmap;
            bgw.ProgressChanged += BackgroundWorker_SingleUse_ProgressChanged;
            bgw.RunWorkerCompleted += BackgroundWorker_SingleUse_RunWorkerCompleted;

            Bitmap currentlyLoadedBitmap = new Bitmap(loadPath);
            bgw.RunWorkerAsync(currentlyLoadedBitmap);
        }
        private void saturateColorsSweep_btn_Click(object sender, EventArgs e)
        {
            //Save location.
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Select Files Save Path";
            string savePath = "C:\\";
            string generalizedSavePath = savePath;
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                savePath = sfd.FileName;
                generalizedSavePath = savePath.Replace(".bmp", ""); //Nip this off to allow generalized filenames.
            }

            //Load location.
            string loadPath = "";
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select File Load Path";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                loadPath = ofd.FileName;
            }

            //BackgroundWorker.
            BackgroundWorker bgw = new BackgroundWorker();

            bgw.WorkerReportsProgress = true;

            bgw.DoWork += BackgroundWorker_1DSweep_SaturateBitmap;
            bgw.ProgressChanged += BackgroundWorker_1DSweep_ProgressChanged;
            bgw.RunWorkerCompleted += BackgroundWorker_1DSweep_RunWorkerCompleted;

            //Was pulling from pictureBox... need better way to store Bitmaps that exist and are modified b/w fns.
            Bitmap currentlyLoadedBitmap = new Bitmap(loadPath);
            bgw.RunWorkerAsync(new object[] { currentlyLoadedBitmap, generalizedSavePath });
        }

        #region Background Worker.

        #region Single-Use.
        private void BackgroundWorker_SingleUse_SaturateBitmap(object sender, DoWorkEventArgs args)
        {
            BackgroundWorker thisWorker = sender as BackgroundWorker;
            Bitmap inputBitmap = (Bitmap)args.Argument;

            //Convert to 64bpp Bitmap.
            Bitmap inputBitmap64;
            BitmapToBitmap_64bppArgb(inputBitmap, out inputBitmap64);
            thisWorker.ReportProgress(16, "Finished converting to 64bpp.");
            
            //Get bytes.
            byte[] inputBitmapBytes = BitmapToByte_64bppArgb(inputBitmap64);
            thisWorker.ReportProgress(32, "Finished converting Bitmap to byte[].");
            
            //Get normalized.
            double[] inputBitmapNormalized = ByteToNormalizedPixels_64bppArgb(inputBitmapBytes);
            thisWorker.ReportProgress(48, "Finished converting byte[] to normalized pixels.");
            
            //Saturate colors.
            ColorVector cv1 = new ColorVector(B1_tb, G1_tb, R1_tb, A1_tb, bias1_tb);
            ColorVector cv2 = new ColorVector(B2_tb, G2_tb, R2_tb, A2_tb, bias2_tb);
            double[] outputBitmapNormalized = SaturateNormalizedPixels(
                inputBitmapNormalized,
                cv1,
                cv2
                );
            thisWorker.ReportProgress(64, "Finished saturating colors.");
            
            //Back to byte[].
            byte[] recoverOutputBitmapBytes = NormalizedPixelsToByte_64bppArgb(outputBitmapNormalized);
            thisWorker.ReportProgress(80, "Finished converting normalized pixels to byte[].");
            
            //Back to Bitmap.
            Bitmap outputBitmap = ByteToBitmap_64bppArgb(recoverOutputBitmapBytes, inputBitmap64);
            args.Result = outputBitmap;
        }

        private void BackgroundWorker_SingleUse_ProgressChanged(object sender, ProgressChangedEventArgs args)
        {
            progress_prg.Value = args.ProgressPercentage;
            progress_lbl.Text = "Progress: " + (string)args.UserState;
        }

        private void BackgroundWorker_SingleUse_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs args)
        {
            progress_prg.Value = 0;
            progress_lbl.Text = "Progress: Done.";
            mainPicture_pb.Image = (Bitmap)args.Result;
        }
        #endregion

        #region 1D-Sweep.
        private void BackgroundWorker_1DSweep_SaturateBitmap(object sender, DoWorkEventArgs args)
        {
            //Recover Bgw args.
            object[] recoveredArgs = args.Argument as object[];
            Bitmap inputBitmap = recoveredArgs[0] as Bitmap;
            string generalizedSavePath = recoveredArgs[1] as string;
            BackgroundWorker thisWorker = sender as BackgroundWorker;

            //Other variables.
            ColorVector[] cvSweep = ColorVector.GenerateColorVectorArray(new List<string> { B1_tb.Text, G1_tb.Text, R1_tb.Text, A1_tb.Text, bias1_tb.Text });
            ColorVector cvConst = new ColorVector(B2_tb, G2_tb, R2_tb, A2_tb, bias2_tb);
            Bitmap inputBitmap64;
            BitmapToBitmap_64bppArgb(inputBitmap, out inputBitmap64);
            
            //Operate.
            for (int n = 0; n < cvSweep.Length; n++)
            {
                using (MemoryStream outputBitmapMemoryStream = new MemoryStream())
                {
                    using (Bitmap outputBitmap = SaturateBitmap(outputBitmapMemoryStream, inputBitmap64, cvConst, cvSweep[n]))
                    {
                        outputBitmap.Save(generalizedSavePath + "_" + Convert.ToString(n) + ".bmp");
                        
                        thisWorker.ReportProgress(
                            (int)((double)n * 100.0 / (double)cvSweep.Length)
                            );
                    }
                }
            }
        }
        
        private void BackgroundWorker_1DSweep_ProgressChanged(object sender, ProgressChangedEventArgs args)
        {
            //Increment GUI.
            progress_prg.Value = args.ProgressPercentage;
            progress_lbl.Text = "Progress: " + args.ProgressPercentage + "%";
        }
        private void BackgroundWorker_1DSweep_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs args)
        {
            progress_prg.Value = 0;
            progress_lbl.Text = "Progress: Done.";
        }
        #endregion

        #endregion

        #region Image Processing.

        #region Block-level Functions.
        public void BitmapToBitmap_64bppArgb(Bitmap argInputBitmap, out Bitmap outputBitmap)
        {
            using (Bitmap intermediateBitmap = new Bitmap(argInputBitmap))
            {
                outputBitmap = intermediateBitmap.Clone(new Rectangle(0, 0, intermediateBitmap.Width, intermediateBitmap.Height), PixelFormat.Format64bppArgb);
            }
            
        }

        public void BitmapToBitmap_24bppRgb(Bitmap argInputBitmap, out Bitmap outputBitmap)
        {
            using (Bitmap intermediateBitmap = new Bitmap(argInputBitmap))
            {
                outputBitmap = intermediateBitmap.Clone(new Rectangle(0, 0, intermediateBitmap.Width, intermediateBitmap.Height), PixelFormat.Format24bppRgb);
            }
        }
        
        public byte[] BitmapToByte_64bppArgb(Bitmap argInputBitmap)
        {
            BitmapData argInputBitmapData = argInputBitmap.LockBits(
                new Rectangle(0, 0, argInputBitmap.Width, argInputBitmap.Height),
                ImageLockMode.ReadWrite,
                PixelFormat.Format64bppArgb
                );

            IntPtr argInputBitmapPtr = argInputBitmapData.Scan0;
            int totalNumBytes = argInputBitmapData.Height * argInputBitmapData.Stride;
            byte[] argInputBitmapBytes = new byte[totalNumBytes];
            Marshal.Copy(argInputBitmapPtr, argInputBitmapBytes, 0, totalNumBytes);

            argInputBitmap.UnlockBits(argInputBitmapData);

            return argInputBitmapBytes;
        }

        public double[] ByteToNormalizedPixels_64bppArgb(byte[] bitmapBytes)
        {
            //Two bytes correspond to one value.
                //Two bytes next to each other have the range [0, 8192.0].
                //Two bytes next to each other represent one RED. One GREEN. etc

            double scalor = 8192.0 / 2;
            double[] normalizedPixels = new double[bitmapBytes.Length >> 1];
            for (int normalized_ind = 0; normalized_ind < normalizedPixels.Length; normalized_ind++)
            {
                int byte_ind = normalized_ind << 1;
                double bytePairValue = (bitmapBytes[byte_ind + 1] << 8) + bitmapBytes[byte_ind];
                
                normalizedPixels[normalized_ind] = (bytePairValue - (scalor)) / scalor;
            }

            return normalizedPixels;
        }

        public double[] SaturateNormalizedPixels(double[] inputNormalizedPixels, ColorVector saturator1, ColorVector saturator2)
        {
            //For each slot in normalizedPixels (section off blocks of 4 at a time in this case).

            double[] outputNormalizedPixels = new double[inputNormalizedPixels.Length];
            for (int n = 0; n < outputNormalizedPixels.Length; n+=4)
            {
                //Make a temporary ColorVector for that slot ([n, n + 3])
                ColorVector inputCurrentPixel = new ColorVector(
                    inputNormalizedPixels[n], 
                    inputNormalizedPixels[n + 1], 
                    inputNormalizedPixels[n + 2],
                    inputNormalizedPixels[n + 3],
                    1
                    );

                double dot1 = inputCurrentPixel.Dot(saturator1); //Dot that temporary ColorVector against saturator1.
                double dot2 = inputCurrentPixel.Dot(saturator2); //Dot that temporary ColorVector against saturator2.

                //Compare these two dot products.
                Array.Copy(dot1 > dot2 ? saturator1.ToArray() : saturator2.ToArray(), 0, outputNormalizedPixels, n, 4);
            }

            return outputNormalizedPixels;
        }

        public byte[] NormalizedPixelsToByte_64bppArgb(double[] normalizedPixels)
        {
            //One double represents two bytes. One double has the range [-1.0, 1.0].

            double scalor = 8192.0 / 2;

            byte[] bitmapBytes = new byte[normalizedPixels.Length << 1];
            for (int normalized_ind = 0; normalized_ind < normalizedPixels.Length; normalized_ind++)
            {
                int byte_ind = normalized_ind << 1;
                ushort bytePairValue = (ushort)(Math.Round(normalizedPixels[normalized_ind] * scalor + scalor));

                bitmapBytes[byte_ind + 1] = (byte)(bytePairValue >> 8); //MSB. Cut terms on right.
                bitmapBytes[byte_ind] = (byte)(bytePairValue & 0x00FF);
            }

            return bitmapBytes;
        }
        
        public Bitmap ByteToBitmap_64bppArgb(byte[] bitmapBytes, Bitmap argInputBitmap)
        {
            //Copy.
            Bitmap localOutputBitmap = (Bitmap)argInputBitmap.Clone(new Rectangle(0,0, argInputBitmap.Width, argInputBitmap.Height), PixelFormat.Format64bppArgb);

            //Lock.
            BitmapData localOutputBitmapData = localOutputBitmap.LockBits(
                new Rectangle(0, 0, localOutputBitmap.Width, localOutputBitmap.Height),
                ImageLockMode.ReadWrite, //problem with lockmode permissions maybe.
                PixelFormat.Format64bppArgb
                );
            
            Marshal.Copy(bitmapBytes, 0, localOutputBitmapData.Scan0, bitmapBytes.Length);

            //Return.
            localOutputBitmap.UnlockBits(localOutputBitmapData);
            return localOutputBitmap;
        }
        #endregion

        public Bitmap SaturateBitmap(MemoryStream outputBitmapMemoryStream, Bitmap inputBitmap64, ColorVector cv1, ColorVector cv2)
        {
            inputBitmap64.Save(outputBitmapMemoryStream, ImageFormat.Bmp);

            //Initialize primitives.
            byte[] currentPixelBytes = new byte[8];
            ColorVector cv = new ColorVector();
            byte[] cvBytesToCopy = new byte[8];

            //Starting at HeaderOffset in inputBitmapMemoryStream.
            outputBitmapMemoryStream.Position = BitmapHeaderLength;
            for (long n = outputBitmapMemoryStream.Position; n < outputBitmapMemoryStream.Length; n += 8)
            {
                //Grab 8 bytes.
                //Convert them to a ColorVector.
                outputBitmapMemoryStream.Read(currentPixelBytes, 0, currentPixelBytes.Length);
                cv = new ColorVector(currentPixelBytes);

                //Dot that ColorVector with cv1 and cv2.
                //For whichever of cv1 and cv2 that has the bigger dot product,
                //Turn that ColorVector into a byte[8].
                cvBytesToCopy = (cv1.Dot(cv) > cv2.Dot(cv)) ? cv1.ToByteArray() : cv2.ToByteArray();

                //Transplant that byte[8] where the original 8 bytes were.
                outputBitmapMemoryStream.Position -= 8;
                outputBitmapMemoryStream.Write(cvBytesToCopy, 0, cvBytesToCopy.Length);
            }

            return new Bitmap(outputBitmapMemoryStream);
        }

        #endregion

        private void autoFillDebug_btn_Click(object sender, EventArgs e)
        {
            B1_tb.Text = "-1.0";
            G1_tb.Text = "-1.0";
            R1_tb.Text = "-1.0";
            A1_tb.Text = "1.0";
            bias1_tb.Text = "-10.0,10.0,500";

            B2_tb.Text = "1.0";
            G2_tb.Text = "1.0";
            R2_tb.Text = "1.0";
            A2_tb.Text = "1.0";
            bias2_tb.Text = "1.0";
        }
    }
}

#region Deprecated
/*
private void BackgroundWorker_1DSweep_SaturateBitmap(object sender, DoWorkEventArgs args)
{
    //1. Need workaround for if pictures are too large.
    //2. Why am I always printing all whites. - Fixed, just a typo in SaturateNormalizedPixels.

    //Takes LinearSpace as Low:High:N from one textbox above.
    //All sweeps have to do with sweeping a ColorVector.
    //So that comes to making a 1D array of color vectors and looping over.

    //Assume ColorVector1 is sweeping.
    //Assume ColorVector2 is constant.

    //BackgroundWorker object recovery.
    object[] recoveredArgs = args.Argument as object[];
    Bitmap inputBitmap = recoveredArgs[0] as Bitmap;
    string generalizedSavePath = recoveredArgs[1] as string;
    BackgroundWorker thisWorker = sender as BackgroundWorker;

    //Generate ColorVectors.
    ColorVector[] cvSweep = ColorVector.GenerateColorVectorArray(new List<string> {B1_tb.Text, G1_tb.Text, R1_tb.Text, A1_tb.Text, bias1_tb.Text});
    ColorVector cvConst = new ColorVector(B2_tb, G2_tb, R2_tb, A2_tb, bias2_tb);

    Bitmap inputBitmap64;
    byte[] inputBitmapBytes;
    double[] inputBitmapNormalized;
    double[] outputBitmapNormalized;
    byte[] recoverOutputBitmapBytes;
    Bitmap outputBitmap;

    //Only need done once.
    //Convert to 64bpp Bitmap.
    inputBitmap64 = BitmapToBitmap_64bppArgb(inputBitmap);

    //Get bytes.
    inputBitmapBytes = BitmapToByte_64bppArgb(inputBitmap64);

    //Get normalized.
    inputBitmapNormalized = ByteToNormalizedPixels_64bppArgb(inputBitmapBytes);

    for (int n = 0; n < cvSweep.Length; n++)
    {
        //Saturate colors.
        outputBitmapNormalized = SaturateNormalizedPixels(
            inputBitmapNormalized,
            cvSweep[n],
            cvConst
            );

        //Back to byte[].
        recoverOutputBitmapBytes = NormalizedPixelsToByte_64bppArgb(outputBitmapNormalized);

        //Back to Bitmap.
        outputBitmap = ByteToBitmap_64bppArgb(recoverOutputBitmapBytes, inputBitmap64);

        thisWorker.ReportProgress(
            (int)((double)n * 100.0 / (double)cvSweep.Length),
            new object[] { outputBitmap, generalizedSavePath, n }
            );
    }
}
*/
#endregion