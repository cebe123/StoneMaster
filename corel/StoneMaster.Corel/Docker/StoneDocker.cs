using System.Windows.Controls;

namespace StoneMaster.Corel.Docker
{
    public partial class StoneDocker : UserControl
    {
        public StoneDocker(object app)
            : this(app, true)
        {
        }

        internal StoneDocker(object app, bool openFloatingWindow)
        {
            MainPlugin.Attach(app);
            InitializeComponent();
        }
    }
}
