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
            DataContext = new UI.StoneDockerViewModel(MainPlugin.Corel, MainPlugin.Engine, MainPlugin.Settings);

            if (openFloatingWindow)
                Loaded += (_, __) => StoneFloatingWindow.Open(MainPlugin.Corel.Application);
        }
    }
}