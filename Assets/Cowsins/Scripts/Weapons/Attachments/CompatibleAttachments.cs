using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace cowsins
{
    /// <summary>
    /// Stores the compatible attachments of a WeaponIdentification object.
    /// </summary>
    [System.Serializable]
    public class CompatibleAttachments
    {
        // Used to Display the Compatible Attachments in the WeaponIdentificationEditor.cs
        // Dictionaries themselves are not serializable in Unity, so they cannot be shown in the Editor just like that.
        [System.Serializable]
        public class CompatibleAttachmentEntry
        {
            public AttachmentType type;
            public Attachment[] attachments;
        }

        // This is a Temporary list used for Editor Display reasons. Attachments during runtime are held by compatibleAttachmentsDict.
        [SerializeField] private List<CompatibleAttachmentEntry> compatibleAttachmentsList = new List<CompatibleAttachmentEntry>();

        private Dictionary<AttachmentType, Attachment[]> compatibleAttachmentsDict;

        // Ensures Dictionary Initialization + Avoids Duplicates 
        private void EnsureDictInitialized()
        {
            if (compatibleAttachmentsDict == null)
            {
                compatibleAttachmentsDict = new Dictionary<AttachmentType, Attachment[]>();
                foreach (var entry in compatibleAttachmentsList)
                {
                    if (entry != null && !compatibleAttachmentsDict.ContainsKey(entry.type))
                    {
                        compatibleAttachmentsDict.Add(entry.type, entry.attachments);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the Compatible Attachment for the Specific Attachment Type given.
        /// </summary>
        /// <param name="type">Attachment Type to return</param>
        /// <returns></returns>
        public IReadOnlyList<Attachment> GetCompatible(AttachmentType type)
        {
            EnsureDictInitialized();

            if (compatibleAttachmentsDict.TryGetValue(type, out var attachments))
                return attachments;

            return new Attachment[0];
        }
    }
}