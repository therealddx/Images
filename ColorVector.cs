using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Images
{
    public class ColorVector
    {
        public enum ColorVectorIndices
        {
            BLUE = 0,
            GREEN = 1,
            RED = 2,
            ALPHA = 3,
            BIAS = 4
        }

        public ColorVector()
        {
            this.B = 1;
            this.G = 1;
            this.R = 1;
            this.A = 1;
            this.bias = 1;
        }

        public double R;
        public double G;
        public double B;
        public double A;
        public double bias;
        public ColorVector(double dB, double dG, double dR, double dA, double dbias)
        {
            this.B = dB;
            this.G = dG;
            this.R = dR;
            this.A = dA;
            this.bias = dbias;
        }
        public ColorVector(byte[] BGRAbytes, double bias = 1, double scalor = 8192/2)
        {
            //byte array will have LSB, then MSB, for BGRA.

            if (BGRAbytes.Length != 8)
            {
                throw new ArgumentException("Error in ColorVector CTOR: BGRAbytes needs have 8 elements exactly.");
            }

            double[] BGRAvalues = new double[5];
            for (int normalized_ind = 0; normalized_ind < 4; normalized_ind++)
            {
                int byte_ind = normalized_ind << 1;
                BGRAvalues[normalized_ind] = (((BGRAbytes[byte_ind + 1] << 8) + BGRAbytes[byte_ind]) - scalor) / scalor;
            }
            BGRAvalues[(int)ColorVectorIndices.BIAS] = bias;

            this.B = BGRAvalues[(int)ColorVectorIndices.BLUE];
            this.G = BGRAvalues[(int)ColorVectorIndices.GREEN];
            this.R = BGRAvalues[(int)ColorVectorIndices.RED];
            this.A = BGRAvalues[(int)ColorVectorIndices.ALPHA];
            this.bias = BGRAvalues[(int)ColorVectorIndices.BIAS];
        }
        public ColorVector(double[] BGRAvalues)
        {
            this.B = BGRAvalues[(int)ColorVectorIndices.BLUE];
            this.G = BGRAvalues[(int)ColorVectorIndices.GREEN];
            this.R = BGRAvalues[(int)ColorVectorIndices.RED];
            this.A = BGRAvalues[(int)ColorVectorIndices.ALPHA];
            this.bias = BGRAvalues[(int)ColorVectorIndices.BIAS];
        }

        public ColorVector(TextBox B_tb, TextBox G_tb, TextBox R_tb, TextBox A_tb, TextBox bias_tb)
        {
            double dB = 0;
            double dG = 0;
            double dR = 0;
            double dA = 0;
            double dbias = 0;

            bool parseB = double.TryParse(B_tb.Text, out dB);
            bool parseG = double.TryParse(G_tb.Text, out dG);
            bool parseR = double.TryParse(R_tb.Text, out dR);
            bool parseA = double.TryParse(A_tb.Text, out dA);
            bool parsebias = double.TryParse(bias_tb.Text, out dbias);

            if (
                (parseB && parseG && parseR && parseA && parsebias) &&
                (BGRAValid(dB, dG, dR, dA))
                )
            {
                this.B = dB;
                this.G = dG;
                this.R = dR;
                this.A = dA;
                this.bias = dbias;
            }
        }

        public static ColorVector[] GenerateColorVectorArray(List<string> textFields)
        {
            //Find which one's the vector.
            int indexOfVector = -1;
            double[] paramSweeper = new double[3]; //L:H:N
            for (int n = 0; n < textFields.Count; n++)
            {
                if (textFields[n].Contains(',')) //That's the sweep.
                {
                    //Note that location.
                    indexOfVector = n;

                    //Make an array out of it.
                    string[] paramSplit = textFields[n].Split(',');
                    for (int m = 0; m < paramSplit.Length; m++)
                    {
                        paramSweeper[m] = Convert.ToDouble(paramSplit[m]);
                    }
                }
            }

            //paramSweeper holds L:H:N. That is, [L,H).
            //Convert this to sweepedParam, which actually represents this linear space.
            double L = paramSweeper[0];
            double H = paramSweeper[1];
            int N = (int)paramSweeper[2];
            double[] sweepedParam = new double[N];
            for (int n = 0; n < N; n++)
            {
                sweepedParam[n] = L + (double)n * (H - L) / (double)N;
            }
            
            //From sweepedParam, generate the List<double[]>, constructorArrays.
            List<double[]> constructorArrays = new List<double[]>();
            for (int n = 0; n < N; n++)
            {
                //Make one constructor array.
                double[] currentConstructorArray = new double[5];
                for (int m = 0; m < currentConstructorArray.Length; m++) //m ranges [0,4].
                {
                    if (m != indexOfVector)
                    {
                        currentConstructorArray[m] = Convert.ToDouble(textFields[m]);
                    }
                    else
                    {
                        currentConstructorArray[m] = sweepedParam[n];
                    }
                }
                constructorArrays.Add(currentConstructorArray);
            }

            //From List<> currentConstructorArrays, generate ColorVector[].
            ColorVector[] cvArray = new ColorVector[N];
            for (int n = 0; n < cvArray.Length; n++)
            {
                cvArray[n] = new ColorVector(constructorArrays[n]);
            }

            return cvArray;
        }

        #region Menials.
        public double Dot(ColorVector otherColorVector)
        {
            return this.bias * otherColorVector.bias * (this.R * otherColorVector.R + this.G * otherColorVector.G + this.B * otherColorVector.B + this.A * otherColorVector.A);
        }
        public double[] ToArray()
        {
            return new double[] { this.B, this.G, this.R, this.A };
        }
        public byte[] ToByteArray(double scalor = 8192.0 / 2)
        {
            //byte array will have LSB, then MSB, for BGRA.

            double[] doubleToReturn = this.ToArray();
            byte[] toReturn = new byte[8];

            for (int n = 0; n < doubleToReturn.Length; n++)
            {
                int byte_ind = n << 1;
                ushort bytePairValue = (ushort)(Math.Round(doubleToReturn[n] * scalor + scalor));
                
                toReturn[byte_ind + 1] = (byte)(bytePairValue >> 8); //MSB. Cut terms on right.
                toReturn[byte_ind] = (byte)(bytePairValue & 0x00FF);
            }

            return toReturn;
        }
        private static bool BGRAValid(double dB, double dG, double dR, double dA)
        {
            return (Math.Abs(dB) <= 1 && Math.Abs(dG) <= 1 && Math.Abs(dR) <= 1 && Math.Abs(dA) <= 1);
        }
        #endregion
    }

    public static class ColorVectors
    {
        public static ColorVector Black = new ColorVector(-1, -1, -1, 1, 1);
        public static ColorVector White = new ColorVector(1, 1, 1, 1, 1);

        public static ColorVector Blue = new ColorVector(1, -1, -1, 1, 1);
        public static ColorVector Green = new ColorVector(-1, 1, -1, 1, 1);
        public static ColorVector Red = new ColorVector(-1, -1, 1, 1, 1);
    }
}
