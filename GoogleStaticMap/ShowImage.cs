using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MapAt
{
    public partial class ShowImage : Form
    {
        public ShowImage()
        {
            InitializeComponent();
        }

        public PictureBox Picture { get { return this.pictureBox1; } }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            this.Close();
        }


        public static void ShowResult(string orig, Bitmap map)
        {
            using (var fm = new ShowImage())
            {
                fm.Picture.Image = map;
                // fm.Show();

                Application.Run(fm);

                //Console.WriteLine("Press [Enter] to continue");
                //Console.ReadLine();
                fm.Close();
            }
        }

    }
}
