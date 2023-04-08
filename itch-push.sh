#!/bin/sh

set -e
VERSION=${VERSION:-$(cat VERSION)}
echo Building $VERSION
echo


# Sets fg color to dim
function dim()
{
	echo -en '\e[2m'
}
# Reset fg color
function resetc()
{
	echo -en '\e[0m'
}

mkdir -p Export/bin Export/dl
cd Export

# Download butler
BUTLER_ZIP=dl/butler.zip

dim
if [ ! $(type -P butler) ]; then
	BUTLER=$(realpath -m ./bin/butler)
	if [ ! -f "$BUTLER"  ]; then
		echo "Downloading Itch.io Butler"

		curl -L -o $BUTLER_ZIP https://broth.itch.ovh/butler/linux-amd64/LATEST/archive/default
		unzip -o -d bin $BUTLER_ZIP
		chmod +x bin/butler
	else
		echo "Butler already downloaded"
	fi
else 
	echo "Butler installed using package manager"
	BUTLER=$(type -P butler)
fi
echo $BUTLER
resetc

echo "Butler version"
$BUTLER -V
echo


# Download Godot
GODOT_VERSION=4.0.1
GODOT_RELEASE_TYPE=stable # stable

if [ ${GODOT_VERSION:0:1} == 4 ]
then
	GODOT_IS_4=1
	GODOT_ARCH=x86_64
else
	GODOT_ARCH=64
fi

# Comment out to disable, don't change the value
GODOT_MONO=1
# 4.0 beta builds don't have headless builds
GODOT_HEADLESS=1
if [[ -v GODOT_IS_4 ]]
then
	GODOT_HEADLESS=
fi

if [ $GODOT_RELEASE_TYPE != "stable" ]
then
	GODOT_RELEASE_TYPE_DIR=$GODOT_RELEASE_TYPE
fi


# Apparently mono builds have a '_' instead of '.' before the architecture 
if [[ -v GODOT_MONO ]]
then
	GODOT_ARCH_SEPARATOR=_
else
	GODOT_ARCH_SEPARATOR=.
fi


GODOT_ZIP=dl/godot.zip
# Mono builds are in a directory, standard builds are in the root of the zip
if [[ -v GODOT_MONO ]]
then
	GODOT_DIR=Godot_v$GODOT_VERSION-${GODOT_RELEASE_TYPE}_${GODOT_MONO:+mono_}linux${GODOT_HEADLESS:+_headless}${GODOT_ARCH_SEPARATOR}$GODOT_ARCH
else
	GODOT_DIR=
fi
GODOT_EXE=Godot_v$GODOT_VERSION-${GODOT_RELEASE_TYPE}_${GODOT_MONO:+mono_}linux${GODOT_HEADLESS:+_headless}.$GODOT_ARCH
GODOT_URL=https://downloads.tuxfamily.org/godotengine/$GODOT_VERSION/$GODOT_RELEASE_TYPE_DIR${GODOT_MONO:+/mono}/$GODOT_DIR.zip

dim
GODOT=$(realpath -m ./bin/$GODOT_DIR/$GODOT_EXE)
if [ ! -f "$GODOT" ]; then
	echo Downloading Godot $GODOT_VERSION $GODOT_RELEASE_TYPE ${GODOT_HEADLESS:+Headless}${GODOT_MONO:+ Mono}...

	curl -L -o $GODOT_ZIP $GODOT_URL
	unzip -o -d bin $GODOT_ZIP
	chmod +x bin/$GODOT_DIR/$GODOT_EXE
else
	echo "Godot already downloaded"
fi

resetc

echo "Godot Version"
set +e
$GODOT --version
set -e
echo


# Export templates
GODOT_TEMPLATES_ZIP=./dl/export_templates.zip
# Godot 4.0 saves templates in 'export_templates', 3.x in 'templates'
if [[ -v GODOT_IS_4 ]]
then
	GODOT_TEMPLATES_DIR_NAME=export_templates
else
	GODOT_TEMPLATES_DIR_NAME=templates
fi
GODOT_TEMPLATES_DIR=~/.local/share/godot/$GODOT_TEMPLATES_DIR_NAME/$GODOT_VERSION.$GODOT_RELEASE_TYPE${GODOT_MONO:+.mono}
GODOT_TEMPLATES_URL=https://downloads.tuxfamily.org/godotengine/$GODOT_VERSION/$GODOT_RELEASE_TYPE_DIR${GODOT_MONO:+/mono}/Godot_v$GODOT_VERSION-${GODOT_RELEASE_TYPE}${GODOT_MONO:+_mono}_export_templates.tpz

dim
if [ ! -d "$GODOT_TEMPLATES_DIR" ]; then
	echo "Downloading export templates for Godot $GODOT_VERSION $GODOT_RELEASE_TYPE${GODOT_MONO:+ Mono}..."

	curl -L -o $GODOT_TEMPLATES_ZIP $GODOT_TEMPLATES_URL
	mkdir -p $GODOT_TEMPLATES_DIR
	unzip -o -d $GODOT_TEMPLATES_DIR $GODOT_TEMPLATES_ZIP
	mv $GODOT_TEMPLATES_DIR/templates/* $GODOT_TEMPLATES_DIR
else
	echo "Export templates already downloaded"
fi
resetc

echo "Export templates version"
cat $GODOT_TEMPLATES_DIR/version.txt
echo


PROJECT=$(realpath -m ..)
echo "Exporting $PROJECT"
echo


# Prepare export presets
# Backup export presets
cp $PROJECT/export_presets.cfg $PROJECT/export_presets.cfg.bak

# Change Windows .exe Version
sed -i "s/application\/file_version=\".*\"/application\/file_version=\"$VERSION\"/" $PROJECT/export_presets.cfg
sed -i "s/application\/product_version=\".*\"/application\/product_version=\"$VERSION\"/" $PROJECT/export_presets.cfg


# Export the game
GAME=MCelium
BUILD_PATH=$(realpath -m ./build)

# GODOT_EXPORT_PRESET_HTML=HTML
GODOT_EXPORT_PRESET_LINUX=Linux64
# GODOT_EXPORT_PRESET_WINDOWS=Windows64

dim
rm -rf $BUILD_PATH

if [[ -v GODOT_IS_4 ]]
then
	GODOT_EXPORT_OPT=--export-release
	GODOT_HEADLESS_OPT=--headless
else
	GODOT_EXPORT_OPT=--export
	GODOT_HEADLESS_OPT=
fi

# HTML
if [ ! -z $GODOT_EXPORT_PRESET_HTML ]
then
	mkdir -p $BUILD_PATH/html
	$GODOT $GODOT_HEADLESS_OPT --path "$PROJECT" $GODOT_EXPORT_OPT HTML "$BUILD_PATH/html/$GAME.html"
	# itch.io expects index.html
	mv "$BUILD_PATH/html/$GAME.html" "$BUILD_PATH/html/index.html"
fi

# Linux x86_64
if [ ! -z $GODOT_EXPORT_PRESET_LINUX ]
then
	mkdir -p $BUILD_PATH/linux64
	$GODOT $GODOT_HEADLESS_OPT --path "$PROJECT" $GODOT_EXPORT_OPT Linux64 "$BUILD_PATH/linux64/$GAME.x86_64"
fi

# Windows x64
if [ ! -z $GODOT_EXPORT_PRESET_WINDOWS ]
then
	mkdir -p $BUILD_PATH/win64
	$GODOT $GODOT_HEADLESS_OPT --path "$PROJECT" $GODOT_EXPORT_OPT Windows64 "$BUILD_PATH/win64/$GAME.exe"
fi

resetc

ITCH_USER=ryhon
ITCH_GAME=mcelium

if [ ! -z $GODOT_EXPORT_PRESET_HTML ]
then
	$BUTLER push $BUILD_PATH/html $ITCH_USER/$ITCH_GAME:html --userversion "$VERSION"
fi
if [ ! -z $GODOT_EXPORT_PRESET_LINUX ]
then
	$BUTLER push $BUILD_PATH/linux64 $ITCH_USER/$ITCH_GAME:linux-64 --userversion "$VERSION"
fi
if [ ! -z $GODOT_EXPORT_PRESET_WINDOWS ]
then
	$BUTLER push $BUILD_PATH/win64 $ITCH_USER/$ITCH_GAME:win-64 --userversion "$VERSION"
fi