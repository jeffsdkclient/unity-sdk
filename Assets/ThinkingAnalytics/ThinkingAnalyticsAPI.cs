﻿/*
 * 
    Copyright 2019, ThinkingData, Inc
    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at

        http://www.apache.org/licenses/LICENSE-2.0

    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
    SDK VERSION:2.2.4
 */
#if !(UNITY_5_4_OR_NEWER)
#define DISABLE_TA
#warning "Your Unity version is not supported by us - ThinkingAnalyticsSDK disabled"
#endif
#if !(UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_WEBGL)
#define DISABLE_TA
#warning "Your Unity Platfrom is not supported by us - ThinkingAnalyticsSDK disabled"
#endif

using System;
using System.Collections.Generic;
using System.Threading;
using ThinkingAnalytics.Utils;
using ThinkingAnalytics.Wrapper;
using UnityEngine;
#if UNITY_EDITOR && UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif
using System.IO;
using ThinkingAnalytics.TaException;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ThinkingAnalytics
{
    /// <summary>
    /// Dynamic super properties interfaces.
    /// </summary>
    public interface IDynamicSuperProperties
    {
        Dictionary<string, object> GetDynamicSuperProperties();
    }

    /// <summary>
    /// 内部使用的特殊事件类， 不要直接使用此类。
    /// </summary>
    public class ThinkingAnalyticsEvent
    {
        public enum Type
        {
            FIRST,
            UPDATABLE,
            OVERWRITABLE
        }

        public ThinkingAnalyticsEvent(string eventName, Dictionary<string, object> properties) {
            EventName = eventName;
            Properties = properties;
        }

        public Type? EventType { get; set; }
        public string EventName { get; }
        public Dictionary<string, object> Properties { get; }

        public DateTime EventTime { get; set; }
        public string ExtraId { get; set; }
    }

    /// <summary>
    /// 首次（唯一）事件。默认情况下采集设备首次事件。请咨询数数客户成功获取支持。
    /// </summary>
    public class TDFirstEvent : ThinkingAnalyticsEvent
    {
        public TDFirstEvent(string eventName, Dictionary<string, object> properties) : base(eventName, properties)
        {
            EventType = Type.FIRST;
        }
        
        /// <summary>
        /// 设置用于检测是否首次的 ID，默认情况下会使用设备 ID
        /// </summary>
        /// <param name="firstCheckId">用于首次事件检测的 ID</param>
        public void SetFirstCheckId(string firstCheckId)
        {
            ExtraId = firstCheckId;
        }
    }

    /// <summary>
    /// 可被更新的事件。请咨询数数客户成功获取支持。
    /// </summary>
    public class TDUpdatableEvent : ThinkingAnalyticsEvent
    {
        public TDUpdatableEvent(string eventName, Dictionary<string, object> properties, string eventId) : base(eventName, properties)
        {
            EventType = Type.UPDATABLE;
            ExtraId = eventId;
        }
    }

    /// <summary>
    /// 可被重写的事件。请咨询数数客户成功获取支持。
    /// </summary>
    public class TDOverWritableEvent : ThinkingAnalyticsEvent
    {
        public TDOverWritableEvent(string eventName, Dictionary<string, object> properties, string eventId) : base(eventName, properties)
        {
            EventType = Type.OVERWRITABLE;
            ExtraId = eventId;
        }
    }

    /// <summary>
    /// 预置属性
    /// </summary>
    public class TDPresetProperties 
    {
        
        public TDPresetProperties(Dictionary<string, object> properties) {
            PresetProperties = properties;
        }
		public string BundleId 
        { 
            get {return (string)PresetProperties["#bundle_id"];}
        }
		public string Carrier 
        { 
            get {return (string)PresetProperties["#carrier"];}
        }
		public string DeviceId
        { 
            get {return (string)PresetProperties["#device_id"];}
        }
		public string DeviceModel 
        { 
            get {return (string)PresetProperties["#device_model"];}
        }
		public string Manufacturer 
        { 
            get {return (string)PresetProperties["#manufacturer"];}
        }
		public string NetworkType 
        { 
            get {return (string)PresetProperties["#network_type"];}
        }
		public string OS 
        { 
            get {return (string)PresetProperties["#os"];}
        }
		public string OSVersion 
        { 
            get {return (string)PresetProperties["#os_version"];}
        }
		public long ScreenHeight 
        { 
            get {return (long)PresetProperties["#screen_height"];}
        }
		public long ScreenWidth 
        { 
            get {return (long)PresetProperties["#screen_width"];}
        }
		public string SystemLanguage 
        { 
            get {return (string)PresetProperties["#system_language"];}
        }
		public double ZoneOffset 
        { 
            get {return (double)PresetProperties["#zone_offset"];}
        }
		private Dictionary<string, object> PresetProperties { get; set; }

        // 返回事件预置属性的Key以"#"开头，不建议直接作为用户属性使用
        public Dictionary<string, object> ToEventPresetProperties()
        {
            return PresetProperties;
        }
         
    }

    // 自动采集事件类型
    [Flags]
    public enum AUTO_TRACK_EVENTS
    {
        NONE = 0,
        APP_START = 1 << 0, // 当应用进入前台的时候触发上报，对应 ta_app_start
        APP_END = 1 << 1, // 当应用进入后台的时候触发上报，对应 ta_app_end
        APP_CRASH = 1 << 4, // 当出现未捕获异常的时候触发上报，对应 ta_app_crash
        APP_INSTALL = 1 << 5, // 应用安装后首次打开的时候触发上报，对应 ta_app_install
        ALL = APP_START | APP_END | APP_INSTALL | APP_CRASH
    }

    public class ThinkingAnalyticsAPI : MonoBehaviour, TaExceptionHandler
    {
        #region settings
        [System.Serializable]
        public struct Token
        {
            public string appid;
            public string serverUrl;
            public TAMode mode;
            public TATimeZone timeZone;
            public string timeZoneId;

            public Token(string appId, string serverUrl, TAMode mode, TATimeZone timeZone, string timeZoneId = null)
            {
                this.appid = appId.Replace(" ", "");
                this.serverUrl = serverUrl;
                this.mode = mode;
                this.timeZone = timeZone;
                this.timeZoneId = timeZoneId;
            }

            public string getTimeZoneId()
            {
                switch (timeZone)
                {
                    case TATimeZone.UTC:
                        return "UTC";
                    case TATimeZone.Asia_Shanghai:
                        return "Asia/Shanghai";
                    case TATimeZone.Asia_Tokyo:
                        return "Asia/Tokyo";
                    case TATimeZone.America_Los_Angeles:
                        return "America/Los_Angeles";
                    case TATimeZone.America_New_York:
                        return "America/New_York";
                    case TATimeZone.Other:
                        return timeZoneId;
                    default:
                        break;
                }
                return null;
            }
        }


        public enum TATimeZone
        {
            Local,
            UTC,
            Asia_Shanghai,
            Asia_Tokyo,
            America_Los_Angeles,
            America_New_York,
            Other = 100
        }

        public enum TAMode
        {
            NORMAL = 0,
            DEBUG = 1,
            DEBUG_ONLY = 2
        }

        public enum NetworkType
        {
            DEFAULT = 1,
            WIFI = 2,
            ALL = 3
        }

        [Header("Configuration")]
        [Tooltip("是否手动初始化SDK")]
        public bool startManually = false;

        [Tooltip("是否打开 Log")]
        public bool enableLog = true;
        [Tooltip("设置网络类型")]
        public NetworkType networkType = NetworkType.DEFAULT;

        [Header("Project")]
        [Tooltip("项目相关配置, APP ID 会在项目申请时给出")]
        [HideInInspector]
        public Token[] tokens = new Token[1];

        #endregion

        public readonly string VERSION = "2.2.4";

        private static ThinkingAnalyticsAPI taAPIInstance;

        //配置Xcode选项
        #if UNITY_EDITOR && UNITY_IOS
        //[PostProcessBuild]
        [PostProcessBuildAttribute(88)]
        public static void onPostProcessBuild(BuildTarget target, string targetPath)
        {            
            if (target != BuildTarget.iOS)
            {
                Debug.LogWarning("Target is not iPhone. XCodePostProcess will not run");
                return;
            }
        
            string projPath = Path.GetFullPath(targetPath) + "/Unity-iPhone.xcodeproj/project.pbxproj";

            UnityEditor.iOS.Xcode.PBXProject proj = new UnityEditor.iOS.Xcode.PBXProject();
            proj.ReadFromFile(projPath);
            #if UNITY_2019_3_OR_NEWER
            string targetGuid = proj.GetUnityFrameworkTargetGuid();
            #else
            string targetGuid = proj.TargetGuidByName(PBXProject.GetUnityTargetName());
            #endif

            //Build Property
            proj.SetBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");//BitCode  NO
            proj.SetBuildProperty(targetGuid, "GCC_ENABLE_OBJC_EXCEPTIONS", "YES");//Enable Objective-C Exceptions

            string[] headerSearchPathsToAdd = { "$(SRCROOT)/Libraries/Plugins/iOS/ThinkingSDK/Source/main", "$(SRCROOT)/Libraries/Plugins/iOS/ThinkingSDK/Source/common" };
            proj.UpdateBuildProperty(targetGuid, "HEADER_SEARCH_PATHS", headerSearchPathsToAdd, null);// Header Search Paths

            //Add Frameworks
            proj.AddFrameworkToProject(targetGuid,"WebKit.framework", true);
            proj.AddFrameworkToProject(targetGuid,"CoreTelephony.framework", true);
            proj.AddFrameworkToProject(targetGuid,"SystemConfiguration.framework", true);
            proj.AddFrameworkToProject(targetGuid,"Security.framework", true);

            //Add Lib
            proj.AddFileToBuild(targetGuid, proj.AddFile("usr/lib/libsqlite3.tbd", "libsqlite3.tbd", PBXSourceTree.Sdk));
            proj.AddFileToBuild(targetGuid, proj.AddFile("usr/lib/libz.tbd", "libz.tbd", PBXSourceTree.Sdk));

            proj.WriteToFile (projPath);
        }        
        #endif

        /// <summary>
        /// 设置自定义访客 ID，用于替换系统生成的访客 ID
        /// </summary>
        /// <param name="FIRSTId">访客 ID</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void Identify(string FIRSTId, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).Identify(FIRSTId);
            }
        }

        /// <summary>
        /// 返回当前的访客 ID.
        /// </summary>
        /// <returns>访客 ID</returns>
        /// <param name="appId">项目 ID(可选)</param>
        public static string GetDistinctId(string appId = "")
        {
            if (tracking_enabled)
            {
                return getInstance(appId).GetDistinctId();
            }
            return null;
        }

        /// <summary>
        /// 设置账号 ID. 该方法不会上传用户登录事件.
        /// </summary>
        /// <param name="account">账号 ID</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void Login(string account, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).Login(account);
            }
        }

        /// <summary>
        /// 清空账号 ID. 该方法不会上传用户登出事件.
        /// </summary>
        /// <param name="appId">项目 ID(可选) </param>
        public static void Logout(string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).Logout();
            }
        }

        /// <summary>
        /// 主动触发上报缓存事件到服务器. 
        /// </summary>
        /// <param name="appId">项目 ID(可选)</param>
        public static void Flush(string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).Flush();

            }
        }

        /// <summary>
        /// 开启自动采集功能.
        /// </summary>
        /// <param name="appId">项目 ID(可选)</param>
        public static void EnableAutoTrack(AUTO_TRACK_EVENTS events, Dictionary<string, object> properties = null, string appId = "")
        {
            if (tracking_enabled)
            {
                if (properties == null)
                {
                    properties = new Dictionary<string, object>();
                }
                getInstance(appId).EnableAutoTrack(events, properties);

                //C#异常捕获提前，包含所有端
                if ((events & AUTO_TRACK_EVENTS.APP_CRASH) != 0)
                {
                    foreach (var item in properties.Keys)
                    {
                        taAPIInstance.autoTrackProperties[item] = properties[item];
                    }
                    ThinkingSDKExceptionHandler eHandler = new ThinkingSDKExceptionHandler();
                    eHandler.SetTaExceptionHandler(taAPIInstance);
                    eHandler.RegisterTaExceptionHandler();
                }
            }
        }

        public static void SetAutoTrackProperties(AUTO_TRACK_EVENTS events, Dictionary<string, object> properties, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).SetAutoTrackProperties(events, properties);
                //C#异常捕获提前，包含所有端
                if ((events & AUTO_TRACK_EVENTS.APP_CRASH) != 0)
                {
                    foreach (var item in properties.Keys)
                    {
                        taAPIInstance.autoTrackProperties[item] = properties[item];
                    }
                }
            }
        }

        /// 异常捕获回调
        public void InvokeTaExceptionHandler(string eventName, Dictionary<string, object> properties)
        {
            foreach (var item in autoTrackProperties.Keys)
            {
                properties[item] = autoTrackProperties[item];
            }
            Track(eventName, properties);
        }

        /// <summary>
        /// track 简单事件. 该事件会先缓存在本地，达到触发上报条件或者主动调用 Flush 时会上报到服务器.
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void Track(string eventName, string appId = "")
        {
            Track(eventName, null, appId);
        }

        /// <summary>
        /// track 事件及事件属性. 该事件会先缓存在本地，达到触发上报条件或者主动调用 Flush 时会上报到服务器.
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="properties">Properties</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void Track(string eventName, Dictionary<string, object> properties, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).Track(eventName, properties);
            }
        }

        /// <summary>
        /// track 事件及事件属性，并指定 #event_time 属性. 该事件会先缓存在本地，达到触发上报条件或者主动调用 Flush 时会上报到服务器. 从 v1.3.0 开始，会考虑 date 的时区信息。支持 UTC 和 local 时区.
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="properties">事件属性</param>
        /// <param name="date">事件时间</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void Track(string eventName, Dictionary<string, object> properties, DateTime date, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).Track(eventName, properties, date);
            }
        }




        public static void Track(ThinkingAnalyticsEvent analyticsEvent, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).Track(analyticsEvent);
            }
        }
        

        /// <summary>
        /// 设置公共事件属性. 公共事件属性指的就是每个事件都会带有的属性.
        /// </summary>
        /// <param name="superProperties">公共事件属性</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void SetSuperProperties(Dictionary<string, object> superProperties, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).SetSuperProperties(superProperties);
            }
        }

        /// <summary>
        /// 删除某个公共事件属性.
        /// </summary>
        /// <param name="property">属性名称</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UnsetSuperProperty(string property, string appId  = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UnsetSuperProperty(property);
            }
        }

        /// <summary>
        /// 返回当前公共事件属性.
        /// </summary>
        /// <returns>公共事件属性</returns>
        /// <param name="appId">项目 ID(可选)</param>
        public static Dictionary<string, object> GetSuperProperties(string appId = "")
        {
            if (tracking_enabled)
            {
                return getInstance(appId).GetSuperProperties();
            }
            return null;
        }

        /// <summary>
        /// 清空公共事件属性.
        /// </summary>
        /// <param name="appId">项目 ID(可选)</param>
        public static void ClearSuperProperties(string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).ClearSuperProperty();
            }
        }

        /// <summary>
        /// 返回事件预置属性
        /// </summary>
        /// <returns>事件预置属性</returns>
        /// <param name="appId">项目 ID(可选)</param>
        public static TDPresetProperties GetPresetProperties(string appId = "")
        {
            if (tracking_enabled)
            {
                Dictionary<string, object> properties = getInstance(appId).GetPresetProperties();
                TDPresetProperties presetProperties = new TDPresetProperties(properties);
                return presetProperties;
            }
            return null;
        }

        /// <summary>
        /// 记录事件时长. 调用 TimeEvent 为某事件开始计时，当 track 传该事件时，SDK 会在在事件属性中加入 #duration 这一属性来表示事件时长，单位为秒.
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void TimeEvent(string eventName, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).TimeEvent(eventName);
            }
        }

        /// <summary>
        /// 设置用户属性. 该接口上传的属性将会覆盖原有的属性值.
        /// </summary>
        /// <param name="properties">用户属性</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserSet(Dictionary<string, object> properties, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserSet(properties);
            }
        }

        /// <summary>
        /// 设置用户属性. 该接口上传的属性将会覆盖原有的属性值.
        /// </summary>
        /// <param name="properties">用户属性</param>
        /// <param name="dateTime">用户属性设置的时间</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserSet(Dictionary<string, object> properties, DateTime dateTime, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserSet(properties, dateTime);
            }
        }

        /// <summary>
        /// 重置一个用户属性.
        /// </summary>
        /// <param name="property">用户属性名称</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserUnset(string property, string appId = "")
        {
            List<string> properties = new List<string>();
            properties.Add(property);
            UserUnset(properties, appId);
        }


        /// <summary>
        /// 重置一组用户属性
        /// </summary>
        /// <param name="properties">用户属性列表</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserUnset(List<string> properties, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserUnset(properties);
            }
        }

        /// <summary>
        /// 重置一组用户属性, 并指定操作时间
        /// </summary>
        /// <param name="properties">用户属性列表</param>
        /// <param name="dateTime">操作时间</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserUnset(List<string> properties, DateTime dateTime, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserUnset(properties, dateTime);
            }
        }

        /// <summary>
        /// 设置用户属性. 当该属性之前已经有值的时候，将会忽略这条信息.
        /// </summary>
        /// <param name="properties">用户属性</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserSetOnce(Dictionary<string, object> properties, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserSetOnce(properties);
            }
        }

        /// <summary>
        /// 设置用户属性. 当该属性之前已经有值的时候，将会忽略这条信息.
        /// </summary>
        /// <param name="properties">用户属性</param>
        /// <param name="dateTime">操作时间</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserSetOnce(Dictionary<string, object> properties, DateTime dateTime, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserSetOnce(properties, dateTime);
            }
        }


        /// <summary>
        /// 对数值类用户属性进行累加. 如果该属性还未被设置，则会赋值 0 后再进行计算.
        /// </summary>
        /// <param name="property">属性名称</param>
        /// <param name="value">数值</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserAdd(string property, object value, string appId = "")
        {
            Dictionary<string, object> properties = new Dictionary<string, object>()
                {
                    { property, value }
                };
            UserAdd(properties, appId);
        }

        /// <summary>
        /// 对数值类用户属性进行累加. 如果属性还未被设置，则会赋值 0 后再进行计算.
        /// </summary>
        /// <param name="properties">用户属性</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserAdd(Dictionary<string, object> properties, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserAdd(properties);
            }
        }

        /// <summary>
        /// 对数值类用户属性进行累加. 如果属性还未被设置，则会赋值 0 后再进行计算.
        /// </summary>
        /// <param name="properties">用户属性</param>
        /// <param name="dateTime">操作时间</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserAdd(Dictionary<string, object> properties, DateTime dateTime, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserAdd(properties, dateTime);
            }
        }

        /// <summary>
        /// 对 List 类型的用户属性进行追加.
        /// </summary>
        /// <param name="properties">用户属性</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserAppend(Dictionary<string, object> properties, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserAppend(properties);
            }
        }

        /// <summary>
        /// 对 List 类型的用户属性进行追加.
        /// </summary>
        /// <param name="properties">用户属性</param>
        /// <param name="dateTime">操作时间</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserAppend(Dictionary<string, object> properties, DateTime dateTime, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserAppend(properties, dateTime);
            }
        }

        /// <summary>
        /// 删除用户数据. 之后再查询该名用户的用户属性，但该用户产生的事件仍然可以被查询到
        /// </summary>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserDelete(string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserDelete();
            }
        }

        /// <summary>
        /// 删除用户数据并指定操作时间.
        /// </summary>
        /// <param name="appId">项目 ID(可选)</param>
        public static void UserDelete(DateTime dateTime, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).UserDelete(dateTime);
            }
        }

        /// <summary>
        /// 设置允许上报数据到服务器的网络类型.
        /// </summary>
        /// <param name="networkType">网络类型</param>
        /// <param name="appId">项目 ID(可选)</param>
        public static void SetNetworkType(NetworkType networkType, string appId =  "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).SetNetworkType(networkType);
            }
        }

        /// <summary>
        /// Gets the device identifier.
        /// </summary>
        /// <returns>The device identifier.</returns>
        public static string GetDeviceId()
        {
            if (tracking_enabled)
            {
                return getInstance("").GetDeviceId();
            } 
            return null;
        }

        /// <summary>
        /// Sets the dynamic super properties.
        /// </summary>
        /// <param name="dynamicSuperProperties">Dynamic super properties interface.</param>
        /// <param name="appId">App ID (optional).</param>
        public static void SetDynamicSuperProperties(IDynamicSuperProperties dynamicSuperProperties, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).SetDynamicSuperProperties(dynamicSuperProperties);
                taAPIInstance.dynamicSuperProperties = dynamicSuperProperties;
            }
        }

        /// <summary>
        /// 停止上报数据，并且清空本地缓存数据(未上报的数据、已设置的访客ID、账号ID、公共属性)
        /// </summary>
        /// <param name="appId">项目ID</param>
        public static void OptOutTracking(string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).OptOutTracking();
            }
        }

        /// <summary>
        /// 停止上报数据，清空本地缓存数据，并且发送 user_del 到服务端.
        /// </summary>
        /// <param name="appId">项目ID</param>
        public static void OptOutTrackingAndDeleteUser(string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).OptOutTrackingAndDeleteUser();
            }
        }

        /// <summary>
        /// 恢复上报数据
        /// </summary>
        /// <param name="appId">项目ID</param>
        public static void OptInTracking(string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).OptInTracking();
            }
        }

        /// <summary>
        /// 暂停/恢复上报数据，本地缓存不会被清空
        /// </summary>
        /// <param name="enabled">是否打开上报数据</param>
        /// <param name="appId">项目ID</param>
        public static void EnableTracking(bool enabled, string appId = "")
        {
            if (tracking_enabled)
            {
                getInstance(appId).EnableTracking(enabled);
            }
        }

        /// <summary>
        /// 创建轻量级实例，轻量级实例与主实例共享项目ID. 访客ID、账号ID、公共属性不共享
        /// </summary>
        /// <param name="appId">项目ID</param>
        /// <returns>轻量级实例的 token </returns>
        public static string CreateLightInstance(string appId = "") {
            if (tracking_enabled)
            {
                if (string.IsNullOrEmpty(appId)) {
                    appId = default_appid;
                }
                ThinkingAnalyticsWrapper lightInstance = getInstance(appId).CreateLightInstance();
                instance_lock.EnterWriteLock();
                try
                {
                    sInstances.Add(lightInstance.GetAppId(), lightInstance);
                } finally
                {
                    instance_lock.ExitWriteLock();
                }
                return lightInstance.GetAppId();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 传入时间戳校准 SDK 时间.
        /// </summary>
        /// <param name="timestamp">当前 Unix timestamp, 单位 毫秒</param>
        public static void CalibrateTime(long timestamp)
        {
            ThinkingAnalyticsWrapper.CalibrateTime(timestamp);
        }

        /// <summary>
        /// 传入 NTP Server 地址校准 SDK 时间.
        ///
        /// 您可以根据您用户所在地传入访问速度较快的 NTP Server 地址, 例如 time.asia.apple.com
        /// SDK 默认情况下会等待 3 秒，去获取时间偏移数据，并用该偏移校准之后的数据.
        /// 如果在 3 秒内未因网络原因未获得正确的时间偏移，本次应用运行期间将不会再校准时间.
        /// </summary>
        /// <param name="timestamp">可用的 NTP 服务器地址</param>
        public static void CalibrateTimeWithNtp(string ntpServer)
        {
            ThinkingAnalyticsWrapper.CalibrateTimeWithNtp(ntpServer);
        }

        //多实例场景,设置默认的appid
        public static void setDefaultAppid(string appid)
        {
            if (sInstances.Count > 0 && sInstances.ContainsKey(appid))
            {
                default_appid = appid;
            }
        }

        #region internal

        public static void StartThinkingAnalytics(Token[] tokens = null) 
        {
            #if DISABLE_TA
            tracking_enabled = false;
            #endif
            TD_Log.EnableLog(taAPIInstance.enableLog);
            ThinkingAnalyticsWrapper.SetVersionInfo(taAPIInstance.VERSION);

            if (tracking_enabled)
            {
                if (tokens == null)
                {
                    tokens = taAPIInstance.tokens;
                }
                default_appid = tokens[0].appid.Replace(" ", "");
                instance_lock.EnterWriteLock();
                try
                {
                    ThinkingAnalyticsWrapper.EnableLog(taAPIInstance.enableLog);
                    foreach (Token token in tokens)
                    {
                        if (!string.IsNullOrEmpty(token.appid))
                        {
                            if (sInstances.ContainsKey(token.appid))
                            {
                                Debug.Log("ThinkingAnalytics is repeated start with appId: "+token.appid);
                            }
                            else 
                            {
                                Token token1 = new Token(token.appid, token.serverUrl, token.mode, token.timeZone, token.timeZoneId);
                                Debug.Log("ThinkingAnalytics start with appId: "+token1.appid);
                                ThinkingAnalyticsWrapper wrapper = new ThinkingAnalyticsWrapper(token1, taAPIInstance);
                                wrapper.SetNetworkType(taAPIInstance.networkType);
                                sInstances.Add(token1.appid,wrapper);
                            }
                        }
                    }
                }
                finally
                {
                    instance_lock.ExitWriteLock();
                }
                if (sInstances.Count == 0)
                {
                    tracking_enabled = false;
                }
            }
        }

        void Awake()
        {
            taAPIInstance = this;

            if (TA_instance == null)
            {
                DontDestroyOnLoad(taAPIInstance.gameObject);
                TA_instance = taAPIInstance;
            } 
            else
            {
                Destroy(taAPIInstance.gameObject);
                return;
            }

            if (startManually == false) 
            {
                ThinkingAnalyticsAPI.StartThinkingAnalytics();
            }
        }

        private void Start() {
            #if UNITY_IOS && !UNITY_EDITOR
            //设置回调托管函数指针
            ResultHandler handler = new ResultHandler(resultHandler);
            IntPtr handlerPointer = Marshal.GetFunctionPointerForDelegate(handler);
            //调用OC的方法，将C#的回调方法函数指针传给OC
            RegisterRecieveGameCallback(handlerPointer);
            #endif
        }

        #if UNITY_IOS && !UNITY_EDITOR
        //声明一个OC的注册回调方法函数指针的函数方法，每一个参数都是函数指针
        [DllImport("__Internal")]
        public static extern void RegisterRecieveGameCallback
        (
            IntPtr handlerPointer
        );    

        //先声明方法、delegate修饰标记是回调方法
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate string ResultHandler(string msg);

        //实现回调方法 MonoPInvokeCallback修饰会让OC通过函数指针回调此方法
        [AOT.MonoPInvokeCallback(typeof(ResultHandler))]
        static string resultHandler(string msg) 
        {
            Debug.Log("收到来自oc的参数  - " + msg);
            Dictionary<string, object>dynamicSuperProperties = taAPIInstance.dynamicSuperProperties.GetDynamicSuperProperties();
            return TD_MiniJSON.Serialize(dynamicSuperProperties);
        }
        #endif 




        private IDynamicSuperProperties dynamicSuperProperties;
        private Dictionary<string, object> autoTrackProperties = new Dictionary<string, object>();
        private static ThinkingAnalyticsAPI TA_instance;
        private static string default_appid; // 如果用户调用接口时不指定项目 ID，默认使用第一个项目 ID
        private static bool tracking_enabled = true;
        private static ReaderWriterLockSlim instance_lock = new ReaderWriterLockSlim();
        private static readonly Dictionary<string, ThinkingAnalyticsWrapper> sInstances = 
            new Dictionary<string, ThinkingAnalyticsWrapper>();

        private static ThinkingAnalyticsWrapper getInstance(string appid)
        {
            instance_lock.EnterReadLock();
            try
            {
                if (sInstances.Count > 0)
                {
                    if (sInstances.ContainsKey(appid)) 
                    {
                        return sInstances[appid];
                    } 
                    return sInstances[default_appid];
                } 
                else 
                {
                    Debug.Log("请先初始化 ThinkingAnalytics SDK");
                    return null;
                }
            } finally
            {
                instance_lock.ExitReadLock();
            }
        }
        #endregion
    }
}
