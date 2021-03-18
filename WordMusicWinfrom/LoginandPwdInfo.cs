using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WordMusicWinfrom
{
    public partial class LoginandPwdInfo : Form
    {
        public LoginandPwdInfo()
        {
            InitializeComponent();
        }

        private void LoginandPwdInfo_Load(object sender, EventArgs e)
        {
            UfullName.Text = UserRegister.id;
            Upwd.Text = UserRegister.pwd;
            Time.Text += UserRegister.time+"今天起";
        }
    }
}
