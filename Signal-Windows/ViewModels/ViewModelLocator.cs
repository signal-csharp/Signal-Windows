using CommonServiceLocator;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Views;
using System;
using System.Collections.Concurrent;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace Signal_Windows.ViewModels
{
    /// <summary>
    /// This class contains static references to all the view models in the
    /// application and provides an entry point for the bindings.
    /// </summary>
    public class ViewModelLocator
    {
        private string Key = Guid.NewGuid().ToString();
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
            SimpleIoc.Default.Register<GlobalSettingsPageViewModel>();
            SimpleIoc.Default.Register<NotificationSettingsPageViewModel>();
            SimpleIoc.Default.Register<PrivacySettingsPageViewModel>();
            SimpleIoc.Default.Register<AppearanceSettingsPageViewModel>();
            SimpleIoc.Default.Register<ChatsAndMediaSettingsPageViewModel>();
            SimpleIoc.Default.Register<AdvancedSettingsPageViewModel>();
            SimpleIoc.Default.Register<DeviceSettingsPageViewmodel>();
            SimpleIoc.Default.Register<BlockedContactsPageViewModel>();
            SimpleIoc.Default.Register<CaptchaPageViewModel>();
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
                return ServiceLocator.Current.GetInstance<MainPageViewModel>(Key.ToString());
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
            get { return ServiceLocator.Current.GetInstance<ConversationSettingsPageViewModel>(Key.ToString()); }
        }

        public GlobalSettingsPageViewModel GlobalSettingsPageInstance
        {
            get { return ServiceLocator.Current.GetInstance<GlobalSettingsPageViewModel>(Key.ToString()); }
        }

        public NotificationSettingsPageViewModel NotificationSettingsPageInstance
        {
            get { return ServiceLocator.Current.GetInstance<NotificationSettingsPageViewModel>(Key.ToString()); }
        }

        public PrivacySettingsPageViewModel PrivacySettingsPageInstance
        {
            get { return ServiceLocator.Current.GetInstance<PrivacySettingsPageViewModel>(Key.ToString()); }
        }

        public AppearanceSettingsPageViewModel AppearanceSettingsPageInstance
        {
            get { return ServiceLocator.Current.GetInstance<AppearanceSettingsPageViewModel>(Key.ToString()); }
        }

        public ChatsAndMediaSettingsPageViewModel ChatsAndMediaSettingsPageInstance
        {
            get { return ServiceLocator.Current.GetInstance<ChatsAndMediaSettingsPageViewModel>(Key.ToString()); }
        }

        public AdvancedSettingsPageViewModel AdvancedSettingsPageInstance
        {
            get { return ServiceLocator.Current.GetInstance<AdvancedSettingsPageViewModel>(Key.ToString()); }
        }

        public DeviceSettingsPageViewmodel DeviceSettingsPageInstance
        {
            get { return ServiceLocator.Current.GetInstance<DeviceSettingsPageViewmodel>(Key.ToString()); }
        }

        public BlockedContactsPageViewModel BlockedContactsPageInstance
        {
            get { return ServiceLocator.Current.GetInstance<BlockedContactsPageViewModel>(Key.ToString()); }
        }

        public CaptchaPageViewModel CaptchaPageInstance
        {
            get { return ServiceLocator.Current.GetInstance<CaptchaPageViewModel>(Key.ToString()); }
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