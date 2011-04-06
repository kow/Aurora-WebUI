<?
//Use gzip if it is supported
if (substr_count($_SERVER['HTTP_ACCEPT_ENCODING'], 'gzip'))
    ob_start("ob_gzhandler"); else
    ob_start();
session_start();

?>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"><html xmlns="http://www.w3.org/1999/xhtml">
<!--<?
/*
 * Copyright (c) 2007 - 2011 Contributors, http://opensimulator.org/, http://aurora-sim.org/
 * See CONTRIBUTORS for a full list of copyright holders.
 *
 * See LICENSE for the full licensing terms of this file.
 *
 */

include("settings/config.php");
include("settings/json.php");
include("settings/mysql.php");
include("check.php");
include("languages/translator.php");
include("templates/templates.php");

if ($_GET[page] != '') {
    $_SESSION[page] = $_GET[page];
} else {
    $_SESSION[page] = 'home';
}

//LOGIN AUTHENTIFICATION
if ($_POST[Submit] == $webui_login) {

    $found = array();
    $found[0] = json_encode(array('Method' => 'Login', 'WebPassword' => md5(WIREDUX_PASSWORD),
                                 'Name' => cleanQuery($_POST[logname]),
                                 'Password' => cleanQuery($_POST[logpassword])));
    $do_post_request = do_post_request($found);
    $recieved = json_decode($do_post_request);
    $UUIDC = $recieved->{'UUID'};
    if ($recieved->{'Verified'} == "true") {
        $_SESSION[USERID] = $UUIDC;
        $_SESSION[NAME] = $_POST[logname];
    } else {
        echo "<script language='javascript'>
		<!--
		alert(\"Sorry, no Account matched\");
		// -->
		</script>";
    }
}

if ($_POST[Submit] == $webui_admin_login) {

    $found = array();
    $found[0] = json_encode(array('Method' => 'AdminLogin', 'WebPassword' => md5(WIREDUX_PASSWORD),
                                 'Name' => $_POST[logname],
                                 'Password' => $_POST[logpassword]));
    $do_post_request = do_post_request($found);
    $recieved = json_decode($do_post_request);
    $UUIDC = $recieved->{'UUID'};
    if ($recieved->{'Verified'} == "true") {
        //Set both the admin and user ids
        $_SESSION[ADMINID] = $UUIDC;
        $_SESSION[USERID] = $UUIDC;
        $_SESSION[NAME] = $_POST[logname];
    } else {
        echo "<script language='javascript'>
		<!--
		alert(\"Sorry, no Admin Account matched\");
		// -->
		</script>";
    }
}
//LOGIN END

$DbLink = new DB;

$DbLink->query("SELECT gridstatus,active,color,title,message  FROM ".C_INFOWINDOW_TBL." ");
list($GRIDSTATUS,$INFOBOX,$BOXCOLOR,$BOX_TITLE,$BOX_INFOTEXT) = $DbLink->next_record();

$found = array();
$found[0] = json_encode(array('Method' => 'OnlineStatus', 'WebPassword' => md5(WIREDUX_PASSWORD)));
$do_post_request = do_post_request($found);
$recieved = json_decode($do_post_request);
$GRIDSTATUS = $recieved->{'Online'};

// Doing it the same as the Who's Online now part
$DbLink = new DB;
$DbLink->query("SELECT UserID FROM " . C_USERINFO_TBL . " where IsOnline = 1 AND " .
        "LastLogin < (UNIX_TIMESTAMP(FROM_UNIXTIME(UNIX_TIMESTAMP(now())))) AND " .
        "LastLogout < (UNIX_TIMESTAMP(FROM_UNIXTIME(UNIX_TIMESTAMP(now())))) " .
        "ORDER BY LastLogin DESC");
$NOWONLINE = 0;
while (list($UUID) = $DbLink->next_record()) {
    // Let's get the user info
    $DbLink2 = new DB;
    $DbLink2->query("SELECT Firstname, Lastname from " . C_USERS_TBL . " where PrincipalID = '" . $UUID . "'");
    list($firstname, $lastname) = $DbLink2->next_record();
    $DbLink3 = new DB;
    $DbLink3->query("SELECT CurrentRegionID from " . C_USERINFO_TBL . " where UserID = '" . $UUID . "'");
    list($regionUUID) = $DbLink3->next_record();
    $username = $firstname . " " . $lastname;
    // Let's get the region information
    $DbLink3 = new DB;
    $DbLink3->query("SELECT RegionName from " . C_REGIONS_TBL . " where RegionUUID = '" . $RegionUUID . "'");
    list($region) = $DbLink3->next_record();
    if ($region != "") {
        $NOWONLINE = $NOWONLINE + 1;
    }
}

$DbLink->query("SELECT count(*) FROM " . C_USERINFO_TBL . " where LastLogin > UNIX_TIMESTAMP(FROM_UNIXTIME(UNIX_TIMESTAMP(now()) - 2419200))");
list($LASTMONTHONLINE) = $DbLink->next_record();

$DbLink->query("SELECT count(*) FROM " . C_USERS_TBL . "");
list($USERCOUNT) = $DbLink->next_record();

$DbLink->query("SELECT count(*) FROM " . C_REGIONS_TBL . "");
list($REGIONSCOUNT) = $DbLink->next_record();
?>-->
<head>
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title><?= SYSNAME ?></title>
    <link rel="stylesheet" href="<? echo $template_css ?>" type="text/css" />
    <link rel="shortcut icon" href="<?=$favicon_image?>" />
	<script src="javascripts/global.js" type="text/javascript"></script>
</head>

<body class="webui">
    
    <div id="bgbody">
        
        <div id="main">
			
            <div id="header">
            	<div id="topcontainer">
            		<div id="translator"><?php include("languages/translator_page.php"); ?></div>
            	</div> <!--fin de #topcontainer -->
                <span style="position:absolute; left:20px; top: 50px;">
                    
                   <a href="<?= SYSURL ?>"><img class="giant" src="<? echo $images_path;?>astragrid_logo.png" border="0" alt="<?= SYSNAME ?>" /></a>
                
                </span>
               
                <div style="position:absolute; right:100px; top:25px; width:auto; height:auto; ">
                
                        <?php include("sites/gridstatus.php"); ?>
                </div>
                <div id="navigation">
                
                    <? include("sites/menubar.php"); ?>
                
                </div>
            
            </div> <!-- end #header -->
            
            <div id="container">
            
            <? include("sites.php"); ?>
            
            </div><!--end #container-->
            
            <div id="endpage"><span></span></div>
            
<div id="footer">
    <?php include("sites/footer.php"); ?>
</div><!-- fin de #footer -->
        </div><!-- end #main -->
    </div><!-- end #bgbody -->
</body>
</html>
