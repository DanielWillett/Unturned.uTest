using System;

namespace uTest.Dummies.Host.Facades;

internal class DummyLocalWorkshopSettings(DummyPlayerHost module) : ILocalWorkshopSettings
{
    public bool getEnabled(PublishedFileId_t fileId)
    {
        return Array.IndexOf(module.WorkshopItems, fileId.m_PublishedFileId) >= 0;
    }

    public void setEnabled(PublishedFileId_t fileId, bool newEnabled) { }
}