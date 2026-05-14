using System.Collections.Generic;

namespace FaceReader_Middleware.Models
{
    /// <summary>
    /// LAN device error code constants and helper for resolving human-readable messages.
    /// Extend this class with additional codes as needed from the device documentation.
    /// </summary>
    public static class LanErrorCodes
    {
        // Success
        public const string Success = "LAN_SUS-0000";

        // General errors (1000-range)
        public const string UnknownAnomaly = "LAN_EXP-1000";
        public const string PasswordError = "LAN_EXP-1001";
        public const string PassParameterAnomaly = "LAN_EXP-1002";
        public const string NoPasswordSet = "LAN_EXP-1003";
        public const string DeviceDisabled = "LAN_EXP-1004";
        public const string DeviceBusy = "LAN_EXP-1005";
        public const string MethodNotSupported = "LAN_EXP-1006";
        public const string DeviceLocked = "LAN_EXP-1007";

        // Password / config related (2000-range - partial, extend as needed)
        public const string OldPassParameterAnomaly = "LAN_EXP-2001";
        public const string NewPassParameterAnomaly = "LAN_EXP-2002";
        public const string PasswordCannotBeNullOrBlank = "LAN_EXP-2003";
        public const string NewPasswordCannotBeNullOrBlank = "LAN_EXP-2004";
        public const string OldPasswordError = "LAN_EXP-2005";
        public const string FirstTimePasswordMismatch = "LAN_EXP-2006";
        public const string ConfigParameterAnomaly = "LAN_EXP-2007";
        public const string ConfigJsonFormatError = "LAN_EXP-2008";
        public const string IllegalJsonParameter = "LAN_EXP-2009";

        // Person / photo related (3000–4000 range - partial)
        public const string PersonParameterAnomaly = "LAN_EXP-3001";
        public const string PersonJsonFormatError = "LAN_EXP-3002";
        public const string PersonIdInvalid = "LAN_EXP-3003";
        public const string PersonNameCannotBeEmpty = "LAN_EXP-3004";
        public const string PersonIdAlreadyExists = "LAN_EXP-3005";
        public const string PersonIdDoesNotExist = "LAN_EXP-3009";

        public const string FaceIdParameterAnomaly = "LAN_EXP-4002";
        public const string PersonIdCharsInvalid = "LAN_EXP-4005";
        public const string FaceIdCharsInvalid = "LAN_EXP-4006";
        public const string FaceIdAlreadyExists = "LAN_EXP-4007";
        public const string ImgBase64CannotBeNull = "LAN_EXP-4008";
        public const string IllegalIsEasyWay = "LAN_EXP-4009";
        public const string ImageAnalysisAnomaly = "LAN_EXP-4011";
        public const string MaxRegisteredPhotosReached = "LAN_EXP-4012";
        public const string PhotoIdNotExist = "LAN_EXP-4017";

        // Recognition records / unix time (5000-range - partial)
        public const string IllegalModelParameter = "LAN_EXP-5007";
        public const string UnixTimeParameterAnomaly = "LAN_EXP-5014";
        public const string UnixTimeFormatError = "LAN_EXP-5015";

        // Result code → message map (partial; extend as needed)
        private static readonly IReadOnlyDictionary<string, string> Messages =
            new Dictionary<string, string>
            {
                [Success] = "Interface called successfully",

                [UnknownAnomaly] = "Unknown anomaly",
                [PasswordError] = "Password error, please check its validity",
                [PassParameterAnomaly] = "pass parameter anomaly",
                [NoPasswordSet] = "No password is set for interface services, please set password first",
                [DeviceDisabled] = "Device is disabled, please enable it first before operating",
                [DeviceBusy] = "Device is busy, please try again later",
                [MethodNotSupported] = "The requested method is not supported",
                [DeviceLocked] = "Device is locked, please try again later",

                [OldPassParameterAnomaly] = "oldPass parameter anomaly",
                [NewPassParameterAnomaly] = "newPass parameter anomaly",
                [PasswordCannotBeNullOrBlank] = "Password cannot be null or with blanks",
                [NewPasswordCannotBeNullOrBlank] = "New password cannot be null or with blanks",
                [OldPasswordError] = "Old password error",
                [FirstTimePasswordMismatch] = "First-time password must have oldPass and newPass equal",
                [ConfigParameterAnomaly] = "config parameter anomaly",
                [ConfigJsonFormatError] = "config JSON format error",
                [IllegalJsonParameter] = "Illegal JSON parameter",

                [PersonParameterAnomaly] = "person parameter anomaly",
                [PersonJsonFormatError] = "Person class JSON format error",
                [PersonIdInvalid] = "Person ID allows only 0–9 and English letters, max length 255",
                [PersonNameCannotBeEmpty] = "name parameter cannot be empty",
                [PersonIdAlreadyExists] = "Personnel ID already exists, please delete or update first",
                [PersonIdDoesNotExist] = "Personnel ID does not exist, please register first",

                [FaceIdParameterAnomaly] = "faceId parameter anomaly",
                [PersonIdCharsInvalid] = "Person ID (personId) allows number 0–9 and English letters only, max length 255",
                [FaceIdCharsInvalid] = "Photo ID (faceId) allows number 0–9 and English letters only, max length 255",
                [FaceIdAlreadyExists] = "Photo ID already exists, please delete or update first",
                [ImgBase64CannotBeNull] = "imgBase64 cannot be null",
                [IllegalIsEasyWay] = "Illegal isEasyWay parameter",
                [ImageAnalysisAnomaly] = "Image analysis anomaly",
                [MaxRegisteredPhotosReached] = "Registration photos already reach the max. number limit",
                [PhotoIdNotExist] = "Photo ID does not exist, please register photo first",

                [IllegalModelParameter] = "Illegal model parameter",
                [UnixTimeParameterAnomaly] = "unixTime parameter anomaly",
                [UnixTimeFormatError] = "unixTime time format error"
            };

        /// <summary>
        /// Returns a human-readable message for a device error code, or the provided default if unknown.
        /// </summary>
        public static string GetMessage(string code, string defaultMessage = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                return defaultMessage ?? "Unknown error code.";

            return Messages.TryGetValue(code, out var msg)
                ? msg
                : defaultMessage ?? code;
        }
    }
}

