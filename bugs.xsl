<?xml version="1.0" encoding="UTF-8"?>

<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns="http://www.w3.org/TR/xhtml1/strict">
<xsl:preserve-space elements="*" />
<xsl:output method="html"/>
<xsl:template match="bug">
<html xml:space="preserve">
<head>
    <title>Bug <xsl:value-of select="bug_id"/>: <xsl:value-of select="short_desc"/></title>
  </head>
  <body bgcolor="#FFFFFF">
    <h1>Bug <xsl:value-of select="bug_id"/>: <xsl:value-of select="short_desc"/></h1> 
   <b><a name="c0">Opened by</a> <xsl:value-of select="reporter" /></b> @ <xsl:value-of select="creation_ts" />
<xsl:for-each select="long_desc">
<pre id="c0"><xsl:apply-templates select="thetext"/></pre>
</xsl:for-each>
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
<br />
	</body>
</html>
</xsl:template>
</xsl:stylesheet>
