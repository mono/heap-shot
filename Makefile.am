
SUBDIRS = HeapShot.Reader HeapShot.Gui.Widgets HeapShot.Gui 

mac-app:
	$(MAKE) -C MacSetup
	rm -rf HeapShot.app
	mv MacSetup/HeapShot.app .