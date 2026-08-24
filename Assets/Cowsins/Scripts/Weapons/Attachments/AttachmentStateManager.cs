using System.Collections.Generic;
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Centralized System for all attachment operations for a specific weapon
    /// </summary>
    public class AttachmentStateManager
    {
        private readonly WeaponIdentification weaponIdentification;
        private readonly Dictionary<AttachmentType, Attachment> currentAttachments;
        private readonly DefaultAttachment defaultAttachments;
        private readonly CompatibleAttachments compatibleAttachments;

        public AttachmentStateManager(WeaponIdentification weaponId, DefaultAttachment defaults, CompatibleAttachments compatible)
        {
            weaponIdentification = weaponId;
            defaultAttachments = defaults;
            compatibleAttachments = compatible;
            
            // Initialize current attachments dictionary
            currentAttachments = new Dictionary<AttachmentType, Attachment>();
            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                currentAttachments[type] = null;
            }
        }

        #region Current State Management

        /// <summary>
        /// Gets the currently equipped attachment of the specified type
        /// </summary>
        public Attachment GetCurrent(AttachmentType type)
        {
            currentAttachments.TryGetValue(type, out var attachment);
            return attachment;
        }

        /// <summary>
        /// Sets the current attachment for the specified type
        /// </summary>
        public void SetCurrent(AttachmentType type, Attachment attachment)
        {
            currentAttachments[type] = attachment;
        }

        /// <summary>
        /// Return all currently equipped attachments
        /// </summary>
        public IReadOnlyDictionary<AttachmentType, Attachment> GetAllCurrent()
        {
            return currentAttachments;
        }

        /// <summary>
        /// Checks if an attachment of the specified type is currently equipped
        /// </summary>
        public bool HasCurrent(AttachmentType type)
        {
            return currentAttachments.ContainsKey(type) && currentAttachments[type] != null;
        }

        /// <summary>
        /// Removes the currently equipped attachment of the specified type
        /// </summary>
        public void RemoveCurrent(AttachmentType type)
        {
            if (currentAttachments.ContainsKey(type)) currentAttachments[type] = null;
        }

        /// <summary>
        /// Deactivates the currently equipped attachment of the specified type
        /// </summary>
        public void DeactivateCurrent(AttachmentType type)
        {
            if (currentAttachments.TryGetValue(type, out var attachment) && attachment != null) attachment.gameObject.SetActive(false);
        }

        #endregion

        #region Default Attachments

        /// <summary>
        /// Gets the default attachment for the specified type
        /// </summary>
        public Attachment GetDefault(AttachmentType type)
        {
            if (defaultAttachments != null && defaultAttachments.DefaultAttachments.TryGetValue(type, out var attachment))
                return attachment;
            return null;
        }

        /// <summary>
        /// Gets all default attachment identifiers
        /// </summary>
        public List<AttachmentIdentifier_SO> GetDefaultIdentifiers()
        {
            var identifiers = new List<AttachmentIdentifier_SO>();

            if (defaultAttachments == null) return identifiers;

            foreach (var kvp in defaultAttachments.DefaultAttachments)
            {
                var attachment = kvp.Value;
                if (attachment != null && attachment.attachmentIdentifier != null)
                {
                    identifiers.Add(attachment.attachmentIdentifier);
                }
            }

            return identifiers;
        }

        /// <summary>
        /// checks if a default attachment exists for the specified type.
        /// </summary>
        public bool HasDefault(AttachmentType type)
        {
            return defaultAttachments?.DefaultAttachments.ContainsKey(type) == true &&
                   defaultAttachments.DefaultAttachments[type] != null;
        }

        #endregion

        #region Compatible Attachments

        /// <summary>
        /// Gets all compatible attachments for the specified type
        /// </summary>
        public IReadOnlyList<Attachment> GetCompatible(AttachmentType type)
        {
            return compatibleAttachments?.GetCompatible(type) ?? new List<Attachment>();
        }

        /// <summary>
        /// Checks if an attachment is compatible with this weapon
        /// </summary>
        public bool IsCompatible(AttachmentIdentifier_SO attachmentIdentifier)
        {
            if (attachmentIdentifier == null || compatibleAttachments == null)
                return false;

            var compatible = compatibleAttachments.GetCompatible(attachmentIdentifier.attachmentType);
            
            foreach (var attachment in compatible)
            {
                if (attachment != null && attachment.attachmentIdentifier == attachmentIdentifier)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the compatible attachment instance for the given identifier
        /// </summary>
        public Attachment FindCompatibleAttachment(AttachmentIdentifier_SO attachmentIdentifier)
        {
            if (attachmentIdentifier == null || compatibleAttachments == null)
                return null;

            var compatible = compatibleAttachments.GetCompatible(attachmentIdentifier.attachmentType);
            
            foreach (var attachment in compatible)
            {
                if (attachment != null && attachment.attachmentIdentifier == attachmentIdentifier)
                    return attachment;
            }

            return null;
        }

        /// <summary>
        /// Gathers the number of compatible attachments for the specified type
        /// </summary>
        public int GetCompatibleCount(AttachmentType type)
        {
            return compatibleAttachments?.GetCompatible(type)?.Count ?? 0;
        }

        #endregion

        #region State Utilities

        /// <summary>
        /// Checks if the current attachment is the default for the specified type.
        /// </summary>
        public bool IsCurrentDefault(AttachmentType type)
        {
            var current = GetCurrent(type);
            var defaultAttachment = GetDefault(type);
            return current == defaultAttachment && current != null;
        }

        /// <summary>
        /// Gets the state of an attachment type
        /// </summary>
        public AttachmentState GetState(AttachmentType type)
        {
            var current = GetCurrent(type);
            
            if (current == null)
                return AttachmentState.None;
            
            var defaultAttachment = GetDefault(type);
            return current == defaultAttachment ? AttachmentState.Default : AttachmentState.Custom;
        }

        /// <summary>
        /// Verifies if any attachment is currently equipped or not
        /// </summary>
        public bool HasAnyAttachment()
        {
            foreach (var kvp in currentAttachments)
            {
                if (kvp.Value != null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the amount of currently equipped attachments.
        /// </summary>
        public int GetCurrentCount()
        {
            int count = 0;
            foreach (var kvp in currentAttachments)
            {
                if (kvp.Value != null)
                    count++;
            }
            return count;
        }

        #endregion
    }

    public enum AttachmentState
    {
        None,       // No attachment equipped
        Default,    // Default attachment equipped
        Custom      // non default attachment equipped
    }
}
