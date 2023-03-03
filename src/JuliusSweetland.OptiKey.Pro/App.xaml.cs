// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using JuliusSweetland.OptiKey.Enums;
using JuliusSweetland.OptiKey.Extensions;
using JuliusSweetland.OptiKey.Models;
using JuliusSweetland.OptiKey.Observables.PointSources;
using JuliusSweetland.OptiKey.Observables.TriggerSources;
using JuliusSweetland.OptiKey.Pro.Properties;
using JuliusSweetland.OptiKey.Services;
using JuliusSweetland.OptiKey.Services.PluginEngine;
using JuliusSweetland.OptiKey.Static;
using JuliusSweetland.OptiKey.UI.ViewModels;
using JuliusSweetland.OptiKey.UI.Windows;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Microsoft.Win32;
using Octokit;
using presage;
using log4net.Appender; //Do not remove even if marked as unused by Resharper - it is used by the Release build configuration
using WindowsRecipes.TaskbarSingleInstance;
using Application = System.Windows.Application;

namespace JuliusSweetland.OptiKey.Pro
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : OptiKeyApp
    {
        private static SplashScreen splashScreen;
        private string startKeyboardOverride = null;

        #region Main
        [STAThread]
        public static void Main(string[] args)
        {
            // Setup derived settings class
            Settings.Initialise();
            String appName = "OptikeyPro";

            Action runApp = () =>
            {
                splashScreen = new SplashScreen("/Resources/Icons/OptikeyProSplash.png");
                splashScreen.Show(false);

                var application = new App(args);
                application.InitializeComponent();
                application.Run();
            };

            if (Settings.Default.AllowMultipleInstances)
            {
                runApp();
            }
            else
            {
                using (_manager = SingleInstanceManager.Initialize(
                    new SingleInstanceManagerSetup(appName)))
                {
                    runApp();
                }
            }
        }
        #endregion

        #region Ctor

        public App(string[] args)
        {
            // Parse command-line args, 
            
            if (args.Length > 0)
            {
                // Allow entry straight into specified dynamic keyboard(s)
                // keyboardArg may be:
                // - single XML file to load
                // - folder containing XML files                

                startKeyboardOverride = args[0];
            }

            // Core setup for all OptiKey apps
            Initialise();

            // (Setup specific to this app happens in App_OnStartup)
        }

        #endregion

        #region On Startup

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            try
            {
                Log.Info("Boot strapping the services and UI.");

                // We manually close this because automatic closure steals focus from the 
                // dynamic splash screen. 
                splashScreen.Close(TimeSpan.FromSeconds(0.5f));

                //Apply theme
                applyTheme();

                //Define MainViewModel before services so I can setup a delegate to call into the MainViewModel
                //This is to work around the fact that the MainViewModel is created after the services.
                MainViewModel mainViewModel = null;
                Action<KeyValue> fireKeySelectionEvent = kv =>
                {
                    if (mainViewModel != null) //Access to modified closure is a good thing here, for once!
                    {
                        mainViewModel.FireKeySelectionEvent(kv);
                    }
                };

                CleanupAndPrepareCommuniKateInitialState();

                ValidateEyeGestures();

                ValidateDynamicKeyboardLocation();

                // Handle plugins. Validate if directory exists and is accessible and pre-load all plugins, building a in-memory list of available ones.
                ValidatePluginsLocation();
                if (Settings.Default.EnablePlugins)
                {
                    PluginEngine.LoadAvailablePlugins();
                }

                var presageInstallationProblem = PresageInstallationProblemsDetected();

                //Create services
                var errorNotifyingServices = new List<INotifyErrors>();
                IAudioService audioService = new AudioService();

                IDictionaryService dictionaryService = new DictionaryService(Settings.Default.SuggestionMethod);
                IPublishService publishService = new PublishService();
                ISuggestionStateService suggestionService = new SuggestionStateService();
                ICalibrationService calibrationService = CreateCalibrationService();
                ICapturingStateManager capturingStateManager = new CapturingStateManager(audioService);
                ILastMouseActionStateManager lastMouseActionStateManager = new LastMouseActionStateManager();
                IKeyStateService keyStateService = new KeyStateService(suggestionService, capturingStateManager,
                    lastMouseActionStateManager, calibrationService, fireKeySelectionEvent);
                IInputService inputService = CreateInputService(keyStateService, dictionaryService, audioService,
                    calibrationService, capturingStateManager, errorNotifyingServices);
                IKeyboardOutputService keyboardOutputService = new KeyboardOutputService(keyStateService,
                    suggestionService, publishService, dictionaryService, fireKeySelectionEvent);
                IMouseOutputService mouseOutputService = new MouseOutputService(publishService);
                errorNotifyingServices.Add(audioService);
                errorNotifyingServices.Add(dictionaryService);
                errorNotifyingServices.Add(publishService);
                errorNotifyingServices.Add(inputService);

                ReleaseKeysOnApplicationExit(keyStateService, publishService);

                //Compose UI
                var mainWindow = new MainWindow(audioService, dictionaryService, inputService, keyStateService);
                IWindowManipulationService mainWindowManipulationService =
                    CreateMainWindowManipulationService(mainWindow);
                errorNotifyingServices.Add(mainWindowManipulationService);
                mainWindow.WindowManipulationService = mainWindowManipulationService;

                //Subscribing to the on closing events.
                mainWindow.Closing += dictionaryService.OnAppClosing;

                mainViewModel = new MainViewModel(
                    audioService, calibrationService, dictionaryService, keyStateService,
                    suggestionService, capturingStateManager, lastMouseActionStateManager,
                    inputService, keyboardOutputService, mouseOutputService, mainWindowManipulationService,
                    errorNotifyingServices, startKeyboardOverride);

                mainWindow.SetMainViewModel(mainViewModel);

                //Setup actions to take once main view is loaded (i.e. the view is ready, so hook up the services which kicks everything off)
                Action postMainViewLoaded = () =>
                {
                    mainViewModel.AttachErrorNotifyingServiceHandlers();
                    mainViewModel.AttachInputServiceEventHandlers();
                };

                mainWindow.AddOnMainViewLoadedAction(postMainViewLoaded);

                //Show the main window
                mainWindow.Show();

                if (Settings.Default.LookToScrollOverlayBoundsThickness > 0
                    || Settings.Default.LookToScrollOverlayDeadzoneThickness > 0)
                {
                    // Create the overlay window, but don't show it yet. It'll make itself visible when the conditions are right.
                    new LookToScrollOverlayWindow(mainViewModel);
                }

                if (Settings.Default.GazeIndicatorStyle != GazeIndicatorStyles.None)
                    new OverlayWindow(mainViewModel);

                //Display splash screen and check for updates (and display message) after the window has been sized and positioned for the 1st time
                EventHandler sizeAndPositionInitialised = null;
                sizeAndPositionInitialised = async (_, __) =>
                {
                    mainWindowManipulationService.SizeAndPositionInitialised -=
                        sizeAndPositionInitialised; //Ensure this handler only triggers once
                    await ShowSplashScreen(inputService, audioService, mainViewModel, OptiKey.Properties.Resources.OPTIKEY_PRO_DESCRIPTION);
                    await mainViewModel.RaiseAnyPendingErrorToastNotifications();
                    await AttemptToStartMaryTTSService(inputService, audioService, mainViewModel);
                    await AlertIfPresageBitnessOrBootstrapOrVersionFailure(presageInstallationProblem, inputService,
                        audioService, mainViewModel);

                    inputService.RequestResume(); //Start the input service

                    await CheckForUpdatesCoder(inputService, audioService, mainViewModel);
                };

                if (mainWindowManipulationService.SizeAndPositionIsInitialised)
                {
                    sizeAndPositionInitialised(null, null);
                }
                else
                {
                    mainWindowManipulationService.SizeAndPositionInitialised += sizeAndPositionInitialised;
                }

                Current.Exit += (o, args) =>
                {
                    mainWindowManipulationService.PersistSizeAndPosition();
                    Settings.Default.Save();
                };
            }
            catch (Exception ex)
            {
                Log.Error("Error starting up application", ex);
                throw;
            }
        }

        protected static async Task<bool> CheckForUpdatesCoder(IInputService inputService, IAudioService audioService, MainViewModel mainViewModel)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>(); //Used to make this method awaitable on the InteractionRequest callback

            try
            {
                if (Settings.Default.CheckForUpdates)
                {
                    string GitHubRepoOwner = "kmcnaught";
                    string GitHubRepoName = "OptikeyCoder";
                    Log.InfoFormat("Checking GitHub for updates (repo owner:'{0}', repo name:'{1}').", GitHubRepoOwner, GitHubRepoName);

                    var github = new GitHubClient(new ProductHeaderValue(GitHubRepoOwner));
                    var releases = await github.Repository.Release.GetAll(GitHubRepoOwner, GitHubRepoName);
                    var latestRelease = releases.FirstOrDefault(release => !release.Prerelease);
                    if (latestRelease != null)
                    {
                        var currentVersion = new Version(DiagnosticInfo.AssemblyVersion); //Convert from string

                        //Discard revision (4th number) as my GitHub releases are tagged with "vMAJOR.MINOR.PATCH"
                        currentVersion = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build);

                        if (!string.IsNullOrEmpty(latestRelease.TagName))
                        {
                            var tagNameWithoutLetters =
                                new string(latestRelease.TagName.ToCharArray().Where(c => !char.IsLetter(c)).ToArray());
                            var latestAvailableVersion = new Version(tagNameWithoutLetters);
                            if (latestAvailableVersion > currentVersion)
                            {
                                if (currentVersion.Major < 4 && latestAvailableVersion.Major >= 4)
                                {
                                    //There should be no update prompt to upgrade from v3 (or earlier) to v4 (or later) due to a breaking change that means that v4 is not
                                    //a suitable choice for many users on earlier version of Optikey (v4 removes supports for Tobii gaming devices).
                                    Log.InfoFormat("An update is available, BUT this update could remove support for the user's current input device. The user will not be notified. " +
                                                   "Current version is {0}. Latest version on GitHub repo is {1}", currentVersion, latestAvailableVersion);
                                }
                                else
                                {
                                    Log.InfoFormat("An update is available. Current version is {0}. Latest version on GitHub repo is {1}",
                                        currentVersion, latestAvailableVersion);

                                    inputService.RequestSuspend();
                                    audioService.PlaySound(Settings.Default.InfoSoundFile, Settings.Default.InfoSoundVolume);
                                    mainViewModel.RaiseToastNotification(OptiKey.Properties.Resources.UPDATE_AVAILABLE,
                                        string.Format(OptiKey.Properties.Resources.URL_DOWNLOAD_PROMPT, latestRelease.TagName),
                                        NotificationTypes.Normal,
                                        () =>
                                        {
                                            inputService.RequestResume();
                                            taskCompletionSource.SetResult(true);
                                        });
                                }
                            }
                            else
                            {
                                Log.Info("No update found.");
                                taskCompletionSource.SetResult(false);
                            }
                        }
                        else
                        {
                            Log.Info("Unable to determine if an update is available as the latest release lacks a tag.");
                            taskCompletionSource.SetResult(false);
                        }
                    }
                    else
                    {
                        Log.Info("No releases found.");
                        taskCompletionSource.SetResult(false);
                    }
                }
                else
                {
                    Log.Info("Check for update is disabled - skipping check.");
                    taskCompletionSource.SetResult(false);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Error when checking for updates. Exception message:{0}\nStackTrace:{1}", ex.Message, ex.StackTrace);
                taskCompletionSource.SetResult(false);
            }

            return await taskCompletionSource.Task;
        }
        #endregion

    }

}
