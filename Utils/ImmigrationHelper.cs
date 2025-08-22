using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EZPlay.Utils
{
    public static class ImmigrationHelper
    {
        private static readonly FieldInfo _carePackagesField;
        private static readonly MethodInfo _selectCarePackageMethod;
        private static readonly MethodInfo _rejectAllCarePackagesMethod;

        private static List<CarePackageInfo> _cachedCarePackages;
        private static float _lastUpdate = float.MinValue;

        static ImmigrationHelper()
        {
            var immigrationType = typeof(Immigration);
            try
            {
                _carePackagesField = immigrationType.GetField("carePackages", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get carePackages field: {ex.Message}");
                _carePackagesField = null;
            }

            try
            {
                _selectCarePackageMethod = immigrationType.GetMethod("SelectCarePackage", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get SelectCarePackage method: {ex.Message}");
                _selectCarePackageMethod = null;
            }

            try
            {
                _rejectAllCarePackagesMethod = immigrationType.GetMethod("RejectAllCarePackages", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get RejectAllCarePackages method: {ex.Message}");
                _rejectAllCarePackagesMethod = null;
            }
        }

        public static List<CarePackageInfo> GetCarePackages()
        {
            if (Time.time - _lastUpdate < 1f && _cachedCarePackages != null)
            {
                return _cachedCarePackages;
            }

            if (Immigration.Instance == null || _carePackagesField == null)
            {
                return new List<CarePackageInfo>();
            }

            try
            {
                var packages = (List<CarePackageInfo>)_carePackagesField.GetValue(Immigration.Instance);
                _cachedCarePackages = packages;
                _lastUpdate = Time.time;
                return packages;
            }
            catch (NullReferenceException ex)
            {
                Debug.LogError($"NullReferenceException in GetCarePackages: {ex.Message}");
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogError($"TargetInvocationException in GetCarePackages: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in GetCarePackages: {ex.Message}");
            }

            return new List<CarePackageInfo>();
        }

        public static void SelectCarePackage(CarePackageInfo info)
        {
            if (Immigration.Instance == null || _selectCarePackageMethod == null)
            {
                return;
            }

            try
            {
                _selectCarePackageMethod.Invoke(Immigration.Instance, new object[] { info });
                _lastUpdate = float.MinValue; // 使缓存失效
            }
            catch (NullReferenceException ex)
            {
                Debug.LogError($"NullReferenceException in SelectCarePackage: {ex.Message}");
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogError($"TargetInvocationException in SelectCarePackage: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in SelectCarePackage: {ex.Message}");
            }
        }

        public static void RejectAllCarePackages()
        {
            if (Immigration.Instance == null || _rejectAllCarePackagesMethod == null)
            {
                return;
            }

            try
            {
                _rejectAllCarePackagesMethod.Invoke(Immigration.Instance, null);
                _lastUpdate = float.MinValue; // 使缓存失效
            }
            catch (NullReferenceException ex)
            {
                Debug.LogError($"NullReferenceException in RejectAllCarePackages: {ex.Message}");
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogError($"TargetInvocationException in RejectAllCarePackages: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in RejectAllCarePackages: {ex.Message}");
            }
        }
    }
}