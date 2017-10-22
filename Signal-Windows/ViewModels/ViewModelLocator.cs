using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Views;
using Microsoft.Practices.ServiceLocation;

namespace Signal_Windows.ViewModels
{
    /// <summary>
    /// This class contains static references to all the view models in the
    /// application and provides an entry point for the bindings.
    /// </summary>
    public class ViewModelLocator
    {
        /// <summary>
        /// Initializes a new instance of the ViewModelLocator class.
        /// </summary>
        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);
            /*if (ViewModelBase.IsInDesignModeStatic)
            {
                // Create design time view services and models
            }
            else
            {
                // Create run time view services and models
            }*/

            //Register your services used here
            SimpleIoc.Default.Register<INavigationService, NavigationService>();
            SimpleIoc.Default.Register<StartPageViewModel>();
            SimpleIoc.Default.Register<AddContactPageViewModel>();
            SimpleIoc.Default.Register<MainPageViewModel>(); 
            SimpleIoc.Default.Register<RegisterPageViewModel>();
            SimpleIoc.Default.Register<RegisterFinalizationPageViewModel>();
            SimpleIoc.Default.Register<LinkPageViewModel>();
            SimpleIoc.Default.Register<FinishRegistrationPageViewModel>();
            SimpleIoc.Default.Register<ConversationSettingsPageViewModel>();
        }

        // <summary>
        // Gets the StartPage view model.
        // </summary>
        // <value>
        // The StartPage view model.
        // </value>
        public StartPageViewModel StartPageInstance
        {
            get
            {
                return ServiceLocator.Current.GetInstance<StartPageViewModel>();
            }
        }

        public AddContactPageViewModel AddContactPageInstance
        {
            get
            {
                return ServiceLocator.Current.GetInstance<AddContactPageViewModel>();
            }
        }

        public MainPageViewModel MainPageInstance
        {
            get
            {
                return ServiceLocator.Current.GetInstance<MainPageViewModel>();
            }
        }

        public RegisterPageViewModel RegisterPageInstance
        {
            get
            {
                return ServiceLocator.Current.GetInstance<RegisterPageViewModel>();
            }
        }

        public RegisterFinalizationPageViewModel RegisterFinalizationPageInstance
        {
            get
            {
                return ServiceLocator.Current.GetInstance<RegisterFinalizationPageViewModel>();
            }
        }

        public LinkPageViewModel LinkPageInstance
        {
            get
            {
                return ServiceLocator.Current.GetInstance<LinkPageViewModel>();
            }
        }

        public FinishRegistrationPageViewModel FinishRegistrationPageInstance
        {
            get { return ServiceLocator.Current.GetInstance<FinishRegistrationPageViewModel>(); }
        }

        public ConversationSettingsPageViewModel ConversationSettingsPageInstance
        {
            get { return ServiceLocator.Current.GetInstance<ConversationSettingsPageViewModel>(); }
        }

        // <summary>
        // The cleanup.
        // </summary>
        public static void Cleanup()
        {
            // TODO Clear the ViewModels
        }
    }
}