using System;
using System.IO;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Common;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Widget;
using Com.Facebook.Network.Connectionclass;
using ipc_offline_app.Droid.BandwidthConnectivity;
using ipc_offline_app.Droid.Helpers;
using ipc_offline_app.Droid.Interface;
using ipc_offline_app.Droid.Portal.Activities;
using ipc_offline_app.Droid.Receivers;
using IpcCommon;
using IpcCommon.Commands;
using IpcCommon.Enumerations;
using IpcCommon.Helper;
using IpcCommon.Model;
using IpcCommon.offline;
using IpcCommon.ViewModel;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Realms;
using Xamarin.Essentials;

namespace ipc_offline_app.Droid.Activities
{
    [Activity(Label = "@string/appName", Icon = "@drawable/icon", ScreenOrientation = ScreenOrientation.Portrait, MainLauncher = true, Theme = "@style/MyTheme.Splash", ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation, NoHistory = true)]
    public class SplashScreenActivity : AppCompatActivity, ITokenReceived
    {
        private Button _retryButton;
        private const int STORAGE_PERMISSION_CODE = 200;
        private Snackbar _rationalSnackBar;
        private int _counter;
        private bool _isfirstrun;
        private RelativeLayout _splashScreenParent;
        private TokenStatusReceiver _receiver;
        
        protected override void OnCreate(Bundle savedInstanceState)
        {
            
            
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.SplashscreenLayout);

            //Adding preferences

            ISharedPreferences pref = Application.Context.GetSharedPreferences("FirstRun", FileCreationMode.Private);
            _isfirstrun = pref.GetBoolean("IsFirstRun", false);
            
            _retryButton = (Button)FindViewById(Resource.Id.retry);
            _splashScreenParent = (RelativeLayout)FindViewById(Resource.Id.splashScreenParent);
            RegisterReceiverForToken();
            NetworkStatusMonitor.GetNetworkStatus().PassContext(this, false, true);
            App.PackageName = ApplicationContext.PackageName;
            _retryButton.Click += (object sender, EventArgs e) =>
            {
                FetchTocken();
            };
            SetupScormEngine();
            CommonUtils.GetInstance().SetStoragePath(Utils.StoragePath());
            CheckForStoragePermision();
            App.Logger = new Logger();
            bool logStatus = DBService.GetDB().GetLogStatus();
            if (logStatus)
                App.LoggerEnabled = true;
            else
                App.LoggerEnabled = false;
            App.PublishEvent = new PublishSyncEvents(ApplicationContext);
            SetDownloadNotificationChannel();
            CheckIfPlayServiceIsAvailable();
        }

        private void FetchTocken()
        {
            _retryButton.Visibility = Android.Views.ViewStates.Invisible;
            SplashViewModel viewModel = new SplashViewModel();
            NavigationCommand navcommand = new NavigationCommand
            {
                callbackListener = Callback,
                IsDomainCall = false,
                IsConnected = Utils.Isconnected()
            };
            viewModel.Execute(navcommand);
        }

        void Callback(APIResponseStatus status, string data)
        {
            RunOnUiThread(() =>
            {
                HandleTokenResponseStatus(status);
            });
        }

        private void ClearPreviousDataOnFirstRun()
        {
            ISharedPreferences prefs = GetSharedPreferences("firstRun", FileCreationMode.Private);
            bool firstRun = prefs.GetBoolean("is_first_run", true);
            bool clearDataForSpecificRelease = DBService.GetDB().ClearExistingData();
            if (firstRun || clearDataForSpecificRelease)
            {
                CommonUtils.GetInstance().ClearAppOfflineData(Utils.StoragePath());
                ISharedPreferencesEditor editor = prefs.Edit();
                editor.PutBoolean("is_first_run", false);
                editor.Apply();
            }
        }

        private void SetupScormEngine(bool update = false)
        {
            string scormPackagePath = FilesDir.AbsolutePath + "/ScormPackage";
            string scormZipPath = FilesDir.AbsolutePath + "/ScormPackage.zip";
            if (update)
                IpcCommon.CommonUtils.GetInstance().DeleteContent(scormPackagePath);
            try
            {
                if (!Directory.Exists(scormPackagePath))
                {
                    using (BinaryReader reader = new BinaryReader(Assets.Open("ScormPackage.zip")))
                    {
                        using (BinaryWriter writer = new BinaryWriter(new FileStream(scormZipPath, FileMode.Create)))
                        {
                            byte[] buffer = new byte[2048];
                            int length = 0;
                            while ((length = reader.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                writer.Write(buffer, 0, length);
                            }
                        }
                    }

                    UnzipHandler unzipHandler = new UnzipHandler(FilesDir.AbsolutePath, scormZipPath, null);
                    unzipHandler.UnzipContent();
                    //delete the .zip file after extraction.
                    Console.WriteLine("");
                }
            }
            catch (Exception e)
            {

            }
        }

        private void SetDownloadNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                NotificationChannel channel = new NotificationChannel(ApplicationContext.PackageName, Const.NOTIFICATION_CHANNEL, Android.App.NotificationImportance.High)
                {
                    Description = Resources.GetString(Resource.String.notification_desc)
                };
                NotificationManager notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);
            }
        }

        protected void CheckForStoragePermision()
        {
            if ((int)Build.VERSION.SdkInt >= 23)
            {
                Permission permission = Android.Support.V4.Content.ContextCompat.CheckSelfPermission(this, Manifest.Permission.ReadExternalStorage);
                if (permission == Permission.Granted)
                {
                    ClearPreviousDataOnFirstRun();
                    LaunchBookshelf();
                }
                else
                {
                    if (ActivityCompat.ShouldShowRequestPermissionRationale(this, Manifest.Permission.WriteExternalStorage))
                    {
                        Snackbar rationalSnackBar = Snackbar.Make(_splashScreenParent, Resource.String.allow_permission_alert, Snackbar.LengthIndefinite).SetAction("", (view) => { });
                        rationalSnackBar.Show();
                    }
                    RequestPermissions(new String[] { Manifest.Permission.ReadExternalStorage, Manifest.Permission.WriteExternalStorage }, STORAGE_PERMISSION_CODE);
                }
            }
            else
            {
                _counter += 1;
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            if (_rationalSnackBar != null && _rationalSnackBar.IsShown)
                _rationalSnackBar.Dismiss();
            switch (requestCode)
            {
                case STORAGE_PERMISSION_CODE:
                    {
                        if (grantResults[0] == Permission.Granted)
                        {
                            ClearPreviousDataOnFirstRun();
                        }
                        else
                        {
                            Snackbar.Make(_splashScreenParent, Resource.String.permission_denied_alert, Snackbar.LengthLong).SetAction("OK", (view) => { }).Show();
                        }
                        LaunchBookshelf();
                        break;
                    }
            }
        }

        public void LaunchBookshelf()
        {
            _counter += 1;
            if (_counter > 1)
            {
                AppUpdateStatus updateStatus = DBService.GetDB().GetAppUpdateStatus();
                if (updateStatus != null && (updateStatus.ShowAppUpdateAlert || updateStatus.UpdateMandatory))
                {
                    Intent updateActivity = new Intent(this, typeof(ForcedEventsActivity));
                    updateActivity.PutExtra(Const.MANDATORY_FLAG, updateStatus.UpdateMandatory);
                    updateActivity.PutExtra(Const.FORCED_EVENT, ForcedEventTypes.APP_UPDATE_EVENT.ToString());
                    StartActivity(updateActivity);
                }
                else
                {
                    //Condtion for Firstruncheck
                    if (_isfirstrun == true)
                    {
                        StartActivity(typeof(OnBoard));
                    }
                    else
                    {
                        StartActivity(typeof(BookshelfActivity));
                    }

                }
            }
        }

        private void LaunchOnBoard()
        {
           
                    StartActivity(typeof(OnBoard));
               
         }
        
        private void RegisterReceiverForToken()
        {
            if (App.TokenFetch == SyncStatus.IN_PROGRESS)
            {
                _receiver = new TokenStatusReceiver(this);
                IntentFilter intentFilter = new IntentFilter();
                intentFilter.AddAction(Constants.TOKEN_FETCH_EVENT);
                LocalBroadcastManager.GetInstance(this).RegisterReceiver(_receiver, intentFilter);
            }
            else
            {
                APIResponseStatus status = (Application as IPCApplication).GetTockenStatus();
                HandleTokenResponseStatus(status);
            }
        }

        private void UnregisterReceiver()
        {
            if (_receiver != null)
            {
                LocalBroadcastManager.GetInstance(this).UnregisterReceiver(_receiver);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnregisterReceiver();
        }

        public void OnTokenReceived(APIResponseStatus status)
        {
            HandleTokenResponseStatus(status);
        }

        private void HandleTokenResponseStatus(APIResponseStatus status)
        {
            if (status == APIResponseStatus.SUCCESS)
            {
                LaunchBookshelf();
            }
            else
            {
                Utils.ShowDialog(this, Resources.GetString(Resource.String.error), Resources.GetString(Resource.String.internet_error),
                                                 Resources.GetString(Resource.String.close));
                _retryButton.Visibility = Android.Views.ViewStates.Visible;
            }
        }

        public void CheckIfPlayServiceIsAvailable()
        {
            int resultCode = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(this);
            string message = "";
            if (resultCode != ConnectionResult.Success)
            {
                if (GoogleApiAvailability.Instance.IsUserResolvableError(resultCode))
                {
                    message = GoogleApiAvailability.Instance.GetErrorString(resultCode);
                }
                else
                {
                    message = Resources.GetString(Resource.String.google_play_error);
                }
                Toast.MakeText(this, message, ToastLength.Long).Show();
            }
        }
    }
}