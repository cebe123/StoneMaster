<?xml version="1.0"?>
<xsl:stylesheet version="1.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:frmwrk="Corel Framework Data">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>
  <frmwrk:uiconfig>
    <frmwrk:applicationInfo userConfiguration="true"/>
  </frmwrk:uiconfig>

  <xsl:template match="node()|@*">
    <xsl:copy><xsl:apply-templates select="node()|@*"/></xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/items">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <itemData
        guid="B0F8A8D1-11D8-4F65-A3B3-05E7F20A3A01"
        type="wpfhost"
        hostedType="Addons\StoneMaster\StoneMaster.Corel.dll,StoneMaster.Corel.Docker.StoneDocker"
        userCaption="StoneMaster"
        dynamicCategory="6D4F4EA3-9D22-44E4-8D61-4C8D11F2D9A2"
        bmpCol="0"
        bmpRow="0"/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>