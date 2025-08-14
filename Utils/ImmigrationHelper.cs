using System;
using System.Collections.Generic;
using System.Reflection;

namespace EZPlay.Utils
{
    public static class ImmigrationHelper
    {
        private static readonly FieldInfo _carePackagesField;
        private static readonly MethodInfo _selectCarePackageMethod;
        private static readonly MethodInfo _rejectAllCarePackagesMethod;

        static ImmigrationHelper()
        {
            var immigrationType = typeof(Immigration);
            _carePackagesField = immigrationType.GetField("carePackages", BindingFlags.NonPublic | BindingFlags.Instance);
            _selectCarePackageMethod = immigrationType.GetMethod("SelectCarePackage", BindingFlags.NonPublic | BindingFlags.Instance);
            _rejectAllCarePackagesMethod = immigrationType.GetMethod("RejectAllCarePackages", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static List<CarePackageInfo> GetCarePackages()
        {
            if (Immigration.Instance == null || _carePackagesField == null)
            {
                return new List<CarePackageInfo>();
            }
            return (List<CarePackageInfo>)_carePackagesField.GetValue(Immigration.Instance);
        }

        public static void SelectCarePackage(CarePackageInfo info)
        {
            if (Immigration.Instance != null && _selectCarePackageMethod != null)
            {
                _selectCarePackageMethod.Invoke(Immigration.Instance, new object[] { info });
            }
        }

        public static void RejectAllCarePackages()
        {
            if (Immigration.Instance != null && _rejectAllCarePackagesMethod != null)
            {
                _rejectAllCarePackagesMethod.Invoke(Immigration.Instance, null);
            }
        }
    }
}