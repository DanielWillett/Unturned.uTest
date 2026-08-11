using System;

namespace uTest.Module;

internal class SingleplayerLocalWorkshopSettings : ILocalWorkshopSettings
{
    internal ulong[] WorkshopItems = Array.Empty<ulong>();

    public bool getEnabled(PublishedFileId_t fileId)
    {
        return Array.IndexOf(WorkshopItems, fileId.m_PublishedFileId) >= 0;
    }

    public void setEnabled(PublishedFileId_t fileId, bool newEnabled) { }
}