using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.View;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using ipc_offline_app.Droid.Fragments;

namespace ipc_offline_app.Droid
{
    [Activity(Label = "On Boarding Screen", Icon = "@drawable/icon", Theme = "@style/Theme.OnBoard")]
    public class OnBoard :AppCompatActivity
    {
        //Button button;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.OnBoardingLayout);

            var toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);
            SupportActionBar.Title = "On Board Screen";

            var viewPager = FindViewById<ViewPager>(Resource.Id.viewPager);
            OnBoardAdapter adapter = new OnBoardAdapter(this);
            viewPager.Adapter = adapter;

            //button = FindViewById<Button>(Resource.Id.button1);
            //button.Click += btn_Hello_Click;

           }

        //    void btn_Hello_Click(object sender, EventArgs e)
        //{
        //    Toast.MakeText(this, "Onborading screen goes here", ToastLength.Short).Show();
        //}

    }
}