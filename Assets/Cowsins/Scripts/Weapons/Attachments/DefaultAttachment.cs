using System.Collections.Generic;
using UnityEngine;

namespace cowsins
{
    [System.Serializable]
    public class DefaultAttachment
    {
        // Used to Display the Default Attachments in the WeaponIdentificationEditor.cs
        // Dictionaries themselves are not serializable in Unity, so they cannot be shown in the Editor just like that.
        [System.Serializable]
        public class AttachmentEntry
        {
            public AttachmentType type;
            public Attachment attachment;
        }

        // This is a Temporary list used for Editor Display reasons. Attachments during runtime are held by defaultAttachments.
        [SerializeField]
        private List<AttachmentEntry> defaultAttachmentsList = new List<AttachmentEntry>();

        // This is the Dictionary that actually contains our runtime attachment Info.
        [System.NonSerialized]
        private Dictionary<AttachmentType, Attachment> defaultAttachments;

        // Default Attachments Getter
        // Ensures Dictionary Initialization + Avoids Duplicates 
        public Dictionary<AttachmentType, Attachment> DefaultAttachments
        {
            get
            {
                if (defaultAttachments == null)
                {
                    defaultAttachments = new Dictionary<AttachmentType, Attachment>();
                    foreach (var entry in defaultAttachmentsList)
                    {
                        if (entry != null && !defaultAttachments.ContainsKey(entry.type))
                            defaultAttachments.Add(entry.type, entry.attachment);
                    }
                }
                return defaultAttachments;
            }
        }
    }
}