﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MSAMISUserInterface {
    public partial class Shadow : Form {
        public Form form { get; set; }

        public Shadow() {
            InitializeComponent();
        }

        private void FadeTMR_Tick(object sender, EventArgs e) {
            this.Opacity += 0.1;
            if (this.Opacity >= 0.6) { FadeTMR.Stop(); form.ShowDialog(); }
        }
        public void Transparent() {
            this.Opacity = 0;
            FadeTMR.Start();
        }


    }
}
