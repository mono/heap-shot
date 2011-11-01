BUNDLE_VERSION=$(VERSION)
MAC_APP_DIR=HeapShot.app
MACOS=$(MAC_APP_DIR)/Contents/MacOS
MAC_APP_LIB_DIR=$(MACOS)/lib/heapshot

EXTRA_DIST = heapshot

all: HeapShot.app

HeapShot.app:
	@echo "Creating $@"
	@mkdir -p $(MAC_APP_DIR)/Contents/{MacOS,Resources}
	@mkdir -p $(MAC_APP_DIR)/Contents/MacOS/{lib,share}
	@mkdir -p $(MAC_APP_LIB_DIR)
	@mkdir -p $(MAC_APP_DIR)/Contents/MacOS/share/heapshot
	@cp -pR ../HeapShot.Gui/*.exe* $(MAC_APP_LIB_DIR)
	@cp -pR ../HeapShot.Gui/*.dll* $(MAC_APP_LIB_DIR)
	@mkdir -p $(MAC_APP_DIR)/Contents/MacOS/share/heapshot
	@cp ../COPYING $(MAC_APP_DIR)/Contents/MacOS/share/heapshot/COPYING
	@sed -e "s/@BUNDLE_VERSION@/$(BUNDLE_VERSION)/g" Info.plist.in > $(MAC_APP_DIR)/Contents/Info.plist
	@cp ../theme-icons/Mac/*.icns $(MAC_APP_DIR)/Contents/Resources/
	@cp -p heapshot $(MACOS)/heapshot
	@touch HeapShot.app

clean-local:
	rm -rf HeapShot.app

.PHONY: HeapShot.app
